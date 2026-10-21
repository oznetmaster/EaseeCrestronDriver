using System.Reflection;

using Crestron.DeviceDrivers.EntityModel;
using Crestron.DeviceDrivers.SDK;

using EaseeChargerClient;

namespace Easee.CrestronDriver.Tests;

[TestFixture, Category ("Processor"), NonParallelizable]
public sealed class ProcessorRuntimeTests
	{
	[SetUp]
	public void RequireProcessorRuntime ()
		{
		if (Type.GetType ("Mono.Runtime") is null)
			Assert.Ignore ("Run the native Crestron SDK lifecycle suite on the processor.");
		}
	[Test]
	public void RootEntityCanBeCreatedDisposedAndRecreatedWithNativeSdk ()
		{
		using var logger = new DriverLogger ("easee-native-tests");
		for (int i = 0; i < 2; i++)
			{
			using var driver = Create (logger);
			var state = driver.GetState ();
			Assert.That (state.PropertyValues["onlineIndicator:isOnline"].GetValue<bool> (), Is.False);
			Assert.That (state.PropertyValues["readyIndicator:isReady"].GetValue<bool> (), Is.False);
			Assert.That (state.Definition.Properties.Keys, Does.Contain ("selectedCharger"));
			Assert.That (state.Definition.Properties.Keys, Does.Contain ("extension:uiDefinition"));
			Assert.That (state.Definition.Properties.Keys, Does.Contain ("siteOptions"));
			}
		}
	[Test]
	public void NativeTokenStoreCommitsThePairAndRefreshMarkerAndClearsOnlyItsOwnRecord ()
		{
		var store = new ProcessorTokenStore ("easee-unit-" + Guid.NewGuid ().ToString ("N"), "synthetic@example.invalid");
		try
			{
			var token = new EaseeToken { AccessToken = "synthetic-access", RefreshToken = "synthetic-refresh", ExpiresAt = DateTimeOffset.UtcNow.AddHours (1) };
			store.SaveAsync (token, CancellationToken.None).GetAwaiter ().GetResult ();
			Assert.That (store.Load ()!.RefreshToken, Is.EqualTo ("synthetic-refresh"));
			token.RequiresReauthentication = true;
			store.SaveAsync (token, CancellationToken.None).GetAwaiter ().GetResult ();
			Assert.That (store.Load ()!.RequiresReauthentication, Is.True);
			}
		finally { store.Clear (); }
		Assert.That (store.Load (), Is.Null);
		}
	[Test]
	public void ChargingEventsAreTelemetryEdgesAndDoNotFireAtStartupOrReconnect ()
		{
		using var logger = new DriverLogger ("easee-edge-tests");
		using var driver = Create (logger);
		int starts = 0, stops = 0;
		driver.ChargingStarted += (_, _) => starts++;
		driver.ChargingStopped += (_, _) => stops++;
		void Publish (ChargerMode mode, bool online = true) => typeof (EaseeDriver).GetMethod ("Publish", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke (driver, [new ChargerReading { Mode = mode, Online = online }, CancellationToken.None]);
		Publish (ChargerMode.Charging);
		Assert.That (starts, Is.Zero);
		Publish (ChargerMode.Completed);
		Publish (ChargerMode.Charging);
		Publish (ChargerMode.Charging);
		Assert.That (starts, Is.EqualTo (1));
		Assert.That (stops, Is.EqualTo (1));
		Publish (ChargerMode.Charging, false);
		Publish (ChargerMode.Completed);
		Assert.That (stops, Is.EqualTo (1));
		}
	[Test]
	public void NativeCommandActivityAccountsForRejectionQueueingAndCancellation ()
		{
		using var logger = new DriverLogger ("easee-activity-tests");
		using var driver = Create (logger);
		Activity Snapshot () => System.Text.Json.JsonSerializer.Deserialize<Activity> (driver.CommandActivity)!;
		string epoch = Snapshot ().Epoch;
		Assert.That (epoch, Is.Not.Empty);
		driver.StartCharging ();
		Assert.That (Snapshot ().Completed, Is.EqualTo (1));
		Assert.That (Snapshot ().Pending, Is.Zero);
		typeof (EaseeDriver).GetMethod ("Publish", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke (driver,
			 [new ChargerReading { Mode = ChargerMode.Completed, Online = true, PowerKilowatts = 0, CableLocked = true }, CancellationToken.None]);
		driver.StartCharging ();
		Assert.That (Snapshot ().Pending, Is.EqualTo (1));
		Assert.That (Snapshot ().Completed, Is.EqualTo (1));
		driver.StopCharging ();
		Assert.That (Snapshot ().Pending, Is.EqualTo (1));
		Assert.That (Snapshot ().Completed, Is.EqualTo (2));
		typeof (EaseeDriver).GetMethod ("SetUnavailable", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke (driver, ["Synthetic loss of connection"]);
		Assert.That (Snapshot ().Pending, Is.Zero);
		Assert.That (Snapshot ().Completed, Is.EqualTo (3));
		Assert.That (Snapshot ().Epoch, Is.EqualTo (epoch));
		Assert.That (driver.CableLockKnown || driver.PowerKnown || driver.ConnectionKnown, Is.False);
		}
	private sealed class Activity
		{
		public string Epoch { get; set; } = "";
		public long Completed
			{
			get; set;
			}
		public int Pending
			{
			get; set;
			}
		}
	[Test]
	public void NativeCheckpointRoundTripsLargeTokensAndTheRefreshMarker ()
		{
		var store = new ProcessorTokenStore ("easee-large-unit-" + Guid.NewGuid ().ToString ("N"), "synthetic@example.invalid");
		try
			{
			var token = new EaseeToken { AccessToken = new string ('a', 6000), RefreshToken = new string ('r', 2000), ExpiresAt = DateTimeOffset.UtcNow.AddHours (1) };
			store.SaveAsync (token, CancellationToken.None).GetAwaiter ().GetResult ();
			Assert.That (store.Load ()!.AccessToken, Is.EqualTo (token.AccessToken));
			Assert.That (store.Load ()!.RefreshToken, Is.EqualTo (token.RefreshToken));
			token.RequiresReauthentication = true;
			store.SaveAsync (token, CancellationToken.None).GetAwaiter ().GetResult ();
			Assert.That (store.Load ()!.RequiresReauthentication, Is.True);
			}
		finally { store.Clear (); }
		Assert.That (store.Load (), Is.Null);
		}
	[Test]
	public void AccountDiscoveryMakesSetupNavigableWithoutEnablingChargerControls ()
		{
		using var logger = new DriverLogger ("easee-selection-tests");
		using var driver = Create (logger);
		typeof (EaseeDriver).GetMethod ("SetSelectionRequired", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke (driver, ["Select a site in Settings"]);
		Assert.That (driver.ReadyIndicatorIsReady && driver.OnlineIndicatorIsOnline, Is.True);
		Assert.That (driver.Online || driver.ControlsEnabled || driver.CanCharge || driver.CanStopCharging || driver.ConnectionKnown, Is.False);
		Assert.That (driver.Status, Is.EqualTo ("Select a site in Settings"));
		}
	[Test]
	public void NativeSelectorsPublishInlineSnapshotsAndRetainAttributedCommands ()
		{
		using var logger = new DriverLogger ("easee-selector-tests");
		using var driver = Create (logger);
		foreach (string id in new[] { "selectedSite", "selectedCharger", "requestedAmps" })
			{
			var initial = driver.GetState ().Definition.Properties[id];
			Assert.That (initial.AvailableValuesProperty, Is.Null);
			Assert.That (initial.TypeDef.AvailableValues.Single ().Value, Is.Empty);
			Assert.That (initial.TypeDef.AvailableValues.Single ().IsUnavailable, Is.True);
			var values = new[] { new Crestron.DeviceDrivers.EntityModel.Data.DriverEntityAvailableValue ("synthetic", new ("Synthetic option", null), false) };
			typeof (EaseeDriver).GetMethod ("PublishSelectorDefinition", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke (driver, [id, values]);
			values[0] = new ("replacement", new ("Replacement", null), false);
			var state = driver.GetState ();
			Assert.That (state.Definition.Properties[id].TypeDef.AvailableValues.Single ().Value, Is.EqualTo ("synthetic"));
			Assert.That (state.Definition.PropertyMetadata[id].ExtensionUiProperty, Is.True);
			Assert.That (state.Definition.Commands.Keys, Does.Contain ("set" + char.ToUpperInvariant (id[0]) + id.Substring (1)));
			typeof (EaseeDriver).GetMethod ("PublishSelectorDefinition", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke (driver, [id, Array.Empty<Crestron.DeviceDrivers.EntityModel.Data.DriverEntityAvailableValue> ()]);
			Assert.That (driver.GetState ().Definition.Properties[id].TypeDef.AvailableValues.Single ().IsUnavailable, Is.True);
			}
		}
	[Test]
	public void ExplicitSelectionSurvivesRecreationIsAccountBoundAndCanClearOnlyTheCharger ()
		{
		string id = "easee-selection-unit-" + Guid.NewGuid ().ToString ("N");
		var store = new ProcessorSelectionStore (id, "synthetic@example.invalid");
		try
			{
			Assert.That (store.Load (), Is.Null);
			store.Save ("synthetic-site", "synthetic-charger");
			var reloaded = new ProcessorSelectionStore (id, "synthetic@example.invalid").Load ();
			Assert.That (reloaded?.Site, Is.EqualTo ("synthetic-site"));
			Assert.That (reloaded?.Charger, Is.EqualTo ("synthetic-charger"));
			Assert.That (new ProcessorSelectionStore (id, "other@example.invalid").Load (), Is.Null);
			store.Save ("synthetic-site", "");
			Assert.That (store.Load ()?.Site, Is.EqualTo ("synthetic-site"));
			Assert.That (store.Load ()?.Charger, Is.Empty);
			}
		finally { store.Clear (); }
		Assert.That (store.Load (), Is.Null);
		}
	private static string DataDirectory ()
		{
		var root = TestContext.Parameters.Get ("TestDataDirectory", TestContext.CurrentContext.TestDirectory);
		var candidates = new[] { Path.Combine (root, "DriverTestData"), Path.Combine (TestContext.CurrentContext.TestDirectory, "DriverTestData"), Path.Combine (TestContext.CurrentContext.TestDirectory, "IncludeInPkg", "DriverTestData") };
		return candidates.FirstOrDefault (Directory.Exists) ?? throw new DirectoryNotFoundException ("The processor package must include the original driver UI test data.");
		}
	private static EaseeDriver Create (DriverLogger logger)
		{
		using var stream = typeof (ProcessorRuntimeTests).Assembly.GetManifestResourceStream ("EaseeDriver.Tests.Manifest")!;
		using var reader = new StreamReader (stream);
		var resources = new DriverImplementationResources
			{
			Logger = logger,
			InitLogger = logger.GetComponentLogger ("easee-test", "driver"),
			DriverDefinition = Serialization.DefinitionFromJsonString (reader.ReadToEnd ()),
			Conditions = new Dictionary<string, ICondition> (),
			Transformations = new Dictionary<string, ITransformation> (),
			TransportConfigItems = new Dictionary<string, IList<ConfigurationItemDefinition>> ()
			};
		var args = new DriverControllerCreationArgs ("easee-unconfigured-unit", DataDirectory (), logger.AppLogger, null);
		return new EaseeDriver (args, resources);
		}
	}
