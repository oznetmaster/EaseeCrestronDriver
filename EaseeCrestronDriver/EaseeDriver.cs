using System.Globalization;
using System.Net;

using Crestron.DeviceDrivers.EntityModel;
using Crestron.DeviceDrivers.EntityModel.Data;
using Crestron.DeviceDrivers.EntityModel.Logging;
using Crestron.DeviceDrivers.SDK;
using Crestron.DeviceDrivers.SDK.EntityModel;
using Crestron.DeviceDrivers.SDK.EntityModel.Data;

using EaseeChargerClient;
using EaseeChargerClient.Models;

namespace Easee.CrestronDriver;

/// <summary>Reports one selected Easee charger and provides explicit charging controls in Crestron Home.</summary>
public sealed partial class EaseeDriver : ReflectedAttributeDriverEntity
	{
	private readonly object _stateLock = new ();
	private readonly object _lifecycleLock = new ();
	private readonly DriverControllerCreationArgs _creation;
	private CancellationTokenSource? _cancellation;
	private Task _worker = Task.CompletedTask;
	private bool _disposed;
	private bool _commandInProgress;
	private string _uiTemplate = "";
	private TilePlacement _placement = TilePlacement.Room;
	private Scheduled_charging_GetScheduleStateResponse? _schedule;
	private DateTimeOffset _nextScheduleRefresh;
	private string _username = "", _password = "";
	private List<Easee_Cloud_API_BasicSiteDTO> _sites = [];
	private List<Easee_Cloud_API_ChargerDTO2> _chargers = [];
	private readonly Queue<Func<ChargerCommands, CancellationToken, Task<string>>> _commands = new ();
	private readonly SemaphoreSlim _wake = new (0, 1);
	private ChargerReading? _previous;

	/// <summary>Creates an instance using the host's configuration, logging and extension resources.</summary>
	/// <param name="creationArgs">Host-assigned identity, data directory and logging services.</param>
	/// <param name="resources">Resources resolved from this driver assembly.</param>
	public EaseeDriver (DriverControllerCreationArgs creationArgs, DriverImplementationResources resources) : base (DriverController.RootControllerId)
		{
		_creation = creationArgs;
		PublishCommandActivity ();
		var configurationArgs = DataDrivenConfigurationControllerArgs.FromResources (creationArgs, resources, ControllerId);
		ConfigurationController = new DelegateDataDrivenConfigurationController (configurationArgs, ApplyConfiguration, null, null);
		var ui = UiDefinitionProperty.LoadFromDirectoryIfExists (creationArgs.DriverDataDirectoryPath, resources.InitLogger, LogEntryLevel.Error);
		_uiTemplate = ui?.GetValue (null, null)?.ToString () ?? "";
		if (!string.IsNullOrEmpty (_uiTemplate))
			ExtensionUiDefinition = TileLayout.Render (_uiTemplate, _placement);
		AddCommand (this, ExtensionDoCommandExecutor.CommandName, new ExtensionDoCommandExecutor (GetCommand, resources.Logger));
		AddCommand (this, ExtensionSetPropertyValueExecutor.CommandName, new ExtensionSetPropertyValueExecutor (GetCommand, resources.Logger));
		InitializeSelectorDefinitions ();
		}
	internal DataDrivenConfigurationController ConfigurationController
		{
		get;
		}

	private ConfigurationItemErrors? ApplyConfiguration (DataDrivenConfigurationController.ApplyConfigurationAction action, string stepId, IDictionary<string, DriverEntityValue?> values)
		{
		if (action == DataDrivenConfigurationController.ApplyConfigurationAction.ClearValues)
			{
			Restart ("", "", "", "", clearSession: true);
			return null;
			}
		try
			{
			_placement = TileLayout.Parse (Get (values, "TilePlacement") ?? (_placement switch
				{
					TilePlacement.Home => "Home page",
					TilePlacement.Both => "Both",
					_ => "Room page"
					}));
			}
		catch (ArgumentException) { return new ConfigurationItemErrors (new Dictionary<string, string> { ["TilePlacement"] = "Choose Room page, Home page or Both." }, "Invalid tile placement."); }
		if (!string.IsNullOrEmpty (_uiTemplate))
			ExtensionUiDefinition = TileLayout.Render (_uiTemplate, _placement);
		string user = Get (values, "Username") ?? _username;
		string password = Get (values, "Password") ?? _password;
		if (string.IsNullOrWhiteSpace (user) || string.IsNullOrEmpty (password))
			return new ConfigurationItemErrors (new Dictionary<string, string> { [string.IsNullOrWhiteSpace (user) ? "Username" : "Password"] = "Enter the Easee account credentials." }, "Account configuration is incomplete.");
		var savedSelection = new ProcessorSelectionStore (_creation.DriverId, user).Load ();
		// The Account step has no site/charger fields. Its reconfiguration must not clear
		// selections made by the extension pickers, including after a reload/backtrack.
		string site = savedSelection?.Site ?? (user == _username ? SelectedSite : Get (values, "SiteId") ?? "");
		string charger = savedSelection?.Charger ?? (user == _username ? SelectedCharger : Get (values, "ChargerId") ?? "");
		if (user == _username && password == _password && site == SelectedSite && charger == SelectedCharger)
			return null;
		Restart (user, password, site, charger,
			 clearSession: !string.IsNullOrEmpty (_username) && (!string.Equals (user, _username, StringComparison.Ordinal) || !string.Equals (password, _password, StringComparison.Ordinal)));
		return null;
		}
	private static string? Get (IDictionary<string, DriverEntityValue?> values, string id) => values.TryGetValue (id, out var value) && value.HasValue ? value.Value.ToString () : null;

	private void Restart (string username, string password, string site, string charger, bool clearSession = false)
		{
		lock (_lifecycleLock)
			{
			if (_disposed)
				return;
			_cancellation?.Cancel ();
			var previous = _worker;
			var oldCancellation = _cancellation;
			_cancellation = new CancellationTokenSource ();
			var token = _cancellation.Token;
			bool accountChanged = !string.Equals (_username, username, StringComparison.Ordinal);
			_username = username;
			_password = password;
			if (accountChanged)
				{
				_sites = [];
				SiteOptions = [];
				PublishSelectorDefinition ("selectedSite", SiteOptions);
				NotifyPropertyChanged ("siteOptions", new DriverEntityValue (SiteOptions));
				}
			_chargers = [];
			ChargerOptions = [];
			PublishSelectorDefinition ("selectedCharger", ChargerOptions);
			NotifyPropertyChanged ("chargerOptions", new DriverEntityValue (ChargerOptions));
			SiteName = "--";
			ChargerName = "--";
			RequestedAmps = "";
			RequestedMinutes = "";
			PublishCurrentOptions (new ChargerReading ());
			lock (_stateLock)
				{
				CancelQueuedCommands ();
				_previous = null;
				_schedule = null;
				_nextScheduleRefresh = DateTimeOffset.MinValue;
				}
			SelectedSite = site;
			SelectedCharger = charger;
			SetUnavailable (string.IsNullOrWhiteSpace (username) ? "Configure the Easee account" : "Connecting");
			_worker = Task.Run (async () =>
				 {
					 try
						 {
						 await previous.ConfigureAwait (false);
						 }
					 catch (Exception) { /* Previous worker reports its own failure. */ }
					 finally { oldCancellation?.Dispose (); }
					 if (clearSession)
						 {
						 try
							 {
							 new ProcessorTokenStore (_creation.DriverId, username).Clear ();
							 clearSession = false;
							 }
						 catch (Exception ex) { if (!token.IsCancellationRequested) ReportFailure (ex); return; }
						 }
					 if (token.IsCancellationRequested)
						 return;
					 while (!token.IsCancellationRequested)
						 {
						 try
							 {
							 await RunAsync (username, password, site, charger, token).ConfigureAwait (false);
							 break;
							 }
						 catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
						 catch (Exception ex) { ReportFailure (ex); }
						 clearSession = false;
						 try
							 {
							 await Task.Delay (TimeSpan.FromSeconds (60), token).ConfigureAwait (false);
							 }
						 catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
						 }
				 });
			}
		}
	private async Task RunAsync (string username, string password, string site, string charger, CancellationToken token)
		{
		var store = new ProcessorTokenStore (_creation.DriverId, username);
		if (string.IsNullOrWhiteSpace (username))
			return;
		using var authentication = new EaseeClient ();
		using var provider = new RecoveringTokenProvider (authentication, username, password, store.SaveAsync, store.Load ());
		using var client = new EaseeClient (new EaseeClientOptions { TokenProvider = provider });
		EaseeRealtimeClient? realtime = null;
		try
			{
			var siteResponse = await client.SiteGetAllAsync (cancellationToken: token).ConfigureAwait (false);
			token.ThrowIfCancellationRequested ();
			_sites = siteResponse.Value ?? [];
			PublishSiteOptions ();
			if (string.IsNullOrEmpty (site))
				{
				SetSelectionRequired ("Select a site in Settings");
				return;
				}
			if (!_sites.Any (s => s.Id?.ToString (CultureInfo.InvariantCulture) == site))
				{
				SetSelectionRequired ("Select an available site in Settings");
				return;
				}
			SiteName = _sites.First (s => s.Id?.ToString (CultureInfo.InvariantCulture) == site).Name ?? site;
			var chargerResponse = await client.SiteGetChargersAsync (site, cancellationToken: token).ConfigureAwait (false);
			token.ThrowIfCancellationRequested ();
			_chargers = chargerResponse.Value ?? [];
			PublishChargerOptions ();
			if (string.IsNullOrEmpty (charger))
				{
				SetSelectionRequired ("Select a charger in Settings");
				return;
				}
			if (!_chargers.Any (c => string.Equals (c.Id, charger, StringComparison.OrdinalIgnoreCase)))
				{
				SetSelectionRequired ("Select an available charger in Settings");
				return;
				}
			ChargerName = _chargers.First (c => string.Equals (c.Id, charger, StringComparison.OrdinalIgnoreCase)).Name ?? charger;
			var gateway = new EaseeGateway (client, charger);
			var commands = new ChargerCommands (gateway);
			realtime = new EaseeRealtimeClient (provider, [charger], update =>
				 {
					 if (!token.IsCancellationRequested && string.Equals (update.DeviceId, charger, StringComparison.OrdinalIgnoreCase))
						 {
						 if (update.ObservationId == 62)
							 {
							 lock (_stateLock)
								 _nextScheduleRefresh = DateTimeOffset.MinValue;
							 RefreshNow ();
							 }
						 if (gateway.Observations.Apply (update))
							 Publish (gateway.Observations.Read (DateTimeOffset.UtcNow), token);
						 }
					 return Task.CompletedTask;
				 });
			while (!token.IsCancellationRequested)
				{
				try
					{
					var reading = await gateway.ReadAsync (token).ConfigureAwait (false);
					bool scheduleDue;
					lock (_stateLock)
						scheduleDue = DateTimeOffset.UtcNow >= _nextScheduleRefresh;
					if (scheduleDue)
						{
						try
							{
							var scheduleResponse = await client.GetChargersSchedulesAsync (charger, cancellationToken: token).ConfigureAwait (false);
							lock (_stateLock)
								_schedule = scheduleResponse.Value;
							}
						catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
						catch (Exception) { lock (_stateLock) _schedule = null; }
						lock (_stateLock)
							_nextScheduleRefresh = DateTimeOffset.UtcNow.AddMinutes (5);
						}
					token.ThrowIfCancellationRequested ();
					Publish (reading, token);
					// Start is idempotent while connected and restores a fully closed push connection.
					try
						{
						await realtime.StartAsync (token).ConfigureAwait (false);
						}
					catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
					catch (Exception) { ConnectionStatus = "REST available; push reconnect pending"; }
					await DrainCommandsAsync (commands, provider, token).ConfigureAwait (false);
					}
				catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
				catch (EaseeApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
					{
					await provider.RequireFreshLoginAsync (token).ConfigureAwait (false);
					SetUnavailable ("Account recovery pending");
					}
				catch (Exception ex) { ReportFailure (ex); }
				await _wake.WaitAsync (TimeSpan.FromSeconds (60), token).ConfigureAwait (false);
				}
			}
		finally
			{
			if (realtime is not null)
				await realtime.DisposeAsync ().ConfigureAwait (false);
			}
		}
	private async Task DrainCommandsAsync (ChargerCommands commands, RecoveringTokenProvider provider, CancellationToken token)
		{
		while (true)
			{
			Func<ChargerCommands, CancellationToken, Task<string>> command;
			lock (_stateLock)
				{
				if (_commands.Count == 0)
					return;
				command = _commands.Dequeue ();
				_commandInProgress = true;
				}
			try
				{
				var result = await command (commands, token).ConfigureAwait (false);
				token.ThrowIfCancellationRequested ();
				CommandStatus = result;
				}
			catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
			catch (ArgumentException) { CommandStatus = "Choose a valid current and positive duration within the reported charger and cable limits"; }
			catch (EaseeApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
				{
				await provider.RequireFreshLoginAsync (token).ConfigureAwait (false);
				CommandStatus = "Sign-in recovery needed; command was not repeated";
				SetUnavailable ("Account recovery pending");
				}
			catch (Exception ex) { CommandStatus = "Command outcome unavailable; check charger status"; ReportFailure (ex); }
			finally { lock (_stateLock) { _commandInProgress = false; _commandCompleted++; PublishCommandActivity (); } }
			}
		}
	private void Enqueue (Func<ChargerCommands, CancellationToken, Task<string>> command)
		{
		lock (_stateLock)
			{
			if (_disposed || !Online)
				{
				CommandStatus = "Charger unavailable";
				_commandCompleted++;
				PublishCommandActivity ();
				return;
				}
			if (_commandInProgress || _commands.Count != 0)
				{
				CommandStatus = "A command is already queued";
				_commandCompleted++;
				PublishCommandActivity ();
				return;
				}
			_commands.Enqueue (command);
			PublishCommandActivity ();
			}
		if (_wake.CurrentCount == 0)
			try
				{
				_wake.Release ();
				}
			catch (SemaphoreFullException) { }
		}
	private void SetSelectionRequired (string message)
		{
		SetUnavailable (message);
		// Account discovery succeeded. The tile must remain usable for selection navigation.
		// These host indicators describe driver availability; charger Online and all controls remain false.
		OnlineIndicatorIsOnline = true;
		ReadyIndicatorIsReady = true;
		TileIcon = "icQuestionmark";
		}
	private void SetUnavailable (string message)
		{
		lock (_stateLock)
			{
			_previous = null;
			if (_commands.Count > 0)
				{
				CancelQueuedCommands ();
				CommandStatus = "Queued command cancelled: charger unavailable";
				}
			}
		ConnectionKnown = false;
		Charging = false;
		PowerKnown = false;
		SessionEnergyKnown = false;
		CableLockKnown = false;
		CableSettingKnown = false;
		PowerDisplay = "--";
		SessionEnergyDisplay = "--";
		TileIcon = "icWifiDisabled";
		OnlineIndicatorIsOnline = false;
		ReadyIndicatorIsReady = false;
		Online = false;
		ControlsEnabled = false;
		CanCharge = false;
		CanStopCharging = false;
		TileDisplay = message;
		Status = message;
		ConnectionStatus = message;
		}
	private void ReportFailure (Exception exception)
		{
		// Exception messages from network/JSON implementations may contain private request data.
		_creation.Logger.Log (_creation.DriverId, LogEntryLevel.Warning, "Easee operation failed (" + exception.GetType ().Name + ").");
		SetUnavailable ("Connection unavailable");
		}
	/// <inheritdoc/>
	public override void Dispose ()
		{
		lock (_lifecycleLock)
			{
			_disposed = true;
			_cancellation?.Cancel ();
			lock (_stateLock)
				CancelQueuedCommands ();
			}
		// The worker owns its clients and finishes cleanup after cancellation; no semaphore is disposed beneath it.
		base.Dispose ();
		}
	private void Update (string id, string value, ref string field)
		{
		lock (_stateLock)
			{
			if (field == value)
				return;
			field = value;
			}
		NotifyPropertyChanged (id, new DriverEntityValue (value));
		}
	private void Update (string id, bool value, ref bool field)
		{
		lock (_stateLock)
			{
			if (field == value)
				return;
			field = value;
			}
		NotifyPropertyChanged (id, new DriverEntityValue (value));
		}
	private void Update (string id, double value, ref double field)
		{
		lock (_stateLock)
			{
			if (field.Equals (value))
				return;
			field = value;
			}
		NotifyPropertyChanged (id, new DriverEntityValue (value));
		}
	}
