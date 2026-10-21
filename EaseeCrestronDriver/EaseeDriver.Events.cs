using Crestron.DeviceDrivers.SDK.EntityModel.Attributes;

namespace Easee.CrestronDriver;

public sealed partial class EaseeDriver
	{
	/// <summary>Raised when a known connected state changes to charging.</summary>
	[EntityEvent (Id = "chargingStarted", FriendlyName = "Charging started")]
	[EntityEventMetadata (Programmable = true)]
	public event EventHandler? ChargingStarted;
	/// <summary>Raised when charging changes to a known noncharging mode.</summary>
	[EntityEvent (Id = "chargingStopped", FriendlyName = "Charging stopped")]
	[EntityEventMetadata (Programmable = true)]
	public event EventHandler? ChargingStopped;
	/// <summary>Raised when a known disconnected state changes to connected.</summary>
	[EntityEvent (Id = "vehicleConnected", FriendlyName = "Vehicle connected")]
	[EntityEventMetadata (Programmable = true)]
	public event EventHandler? VehicleConnectedEvent;
	/// <summary>Raised when a known connected state changes to disconnected.</summary>
	[EntityEvent (Id = "vehicleDisconnected", FriendlyName = "Vehicle disconnected")]
	[EntityEventMetadata (Programmable = true)]
	public event EventHandler? VehicleDisconnected;
	/// <summary>Raised when the charger changes from online to offline.</summary>
	[EntityEvent (Id = "chargerOffline", FriendlyName = "Charger offline")]
	[EntityEventMetadata (Programmable = true)]
	public event EventHandler? ChargerOffline;
	/// <summary>Raised when the charger changes from offline to online.</summary>
	[EntityEvent (Id = "chargerOnline", FriendlyName = "Charger online")]
	[EntityEventMetadata (Programmable = true)]
	public event EventHandler? ChargerOnline;
	/// <summary>Raised when a known nonerror mode changes to the charger error mode.</summary>
	[EntityEvent (Id = "chargerError", FriendlyName = "Charger error")]
	[EntityEventMetadata (Programmable = true)]
	public event EventHandler? ChargerError;

	private void Publish (ChargerReading reading, CancellationToken cancellationToken = default)
		{
		lock (_stateLock)
			{
			if (_disposed || cancellationToken.IsCancellationRequested)
				return;
			var schedule = SchedulePlanner.Next (_schedule, DateTimeOffset.UtcNow);
			reading = reading with
				{
				NextScheduledStart = schedule.Instant,
				ScheduledStartDisplay = schedule.Display
				};
			NextStartDisplay = schedule.Detail;
			var previous = _previous;
			Online = reading.Online == true;
			OnlineIndicatorIsOnline = Online;
			ReadyIndicatorIsReady = Online && reading.ModeKnown && reading.Mode != ChargerMode.Error;
			ControlsEnabled = Online;
			CanCharge = reading.CanStart;
			CanStopCharging = reading.CanStop;
			ConnectionKnown = Online && reading.VehicleConnected.HasValue;
			VehicleConnected = Online && reading.VehicleConnected == true;
			Charging = Online && reading.Charging;
			Status = reading.Status;
			TileDisplay = reading.TileStatus;
			TileIcon = !Online ? "icWifiDisabled" : !reading.ModeKnown ? "icQuestionmark" : reading.Mode == ChargerMode.Error ? "icAlertRegular" : reading.Charging ? "icQuickAction" : reading.VehicleConnected == true ? "icVehicleOn" : "icVehicleOff";
			PowerKnown = Online && reading.PowerKilowatts.HasValue;
			PowerKilowatts = reading.PowerKilowatts ?? 0;
			PowerDisplay = Online ? reading.PowerDisplay : "--";
			SessionEnergyKnown = reading.SessionEnergyKilowattHours.HasValue;
			SessionEnergy = reading.SessionEnergyKilowattHours ?? 0;
			SessionEnergyDisplay = reading.SessionEnergyDisplay;
			LifetimeEnergyDisplay = ChargerReading.Format (reading.LifetimeEnergyKilowattHours, "kWh");
			CableLockKnown = Online && reading.CableLocked.HasValue;
			CableLocked = reading.CableLocked == true;
			CableSettingKnown = Online && reading.KeepCableLocked.HasValue;
			KeepCableLocked = reading.KeepCableLocked == true;
			CurrentLimitDisplay = Online ? ChargerReading.Format (reading.AvailableCurrentAmps, "A") : "--";
			PublishCurrentOptions (Online ? reading : new ChargerReading ());
			ConnectionStatus = Online ? "Connected to Easee" : "Charger offline";
			_previous = reading;
			if (previous is null)
				return;
			if (previous.Online == true && reading.Online == false)
				ChargerOffline?.Invoke (this, EventArgs.Empty);
			if (previous.Online == false && reading.Online == true)
				ChargerOnline?.Invoke (this, EventArgs.Empty);
			// Offline/reconnect snapshots establish a new baseline, not fictitious vehicle/charging edges.
			if (previous.Online != true || reading.Online != true || !previous.ModeKnown || !reading.ModeKnown)
				return;
			if (!previous.Charging && reading.Charging)
				ChargingStarted?.Invoke (this, EventArgs.Empty);
			if (previous.Charging && !reading.Charging)
				ChargingStopped?.Invoke (this, EventArgs.Empty);
			if (previous.VehicleConnected == false && reading.VehicleConnected == true)
				VehicleConnectedEvent?.Invoke (this, EventArgs.Empty);
			if (previous.VehicleConnected == true && reading.VehicleConnected == false)
				VehicleDisconnected?.Invoke (this, EventArgs.Empty);
			if (previous.Mode != ChargerMode.Error && reading.Mode == ChargerMode.Error)
				ChargerError?.Invoke (this, EventArgs.Empty);
			}
		}
	}