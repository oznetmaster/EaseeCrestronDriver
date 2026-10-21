using System.Globalization;

using EaseeChargerClient.Models;

namespace Easee.CrestronDriver;

// All fields represent actual observations. Missing values stay unknown.
internal sealed record ChargerReading
	{
	internal ChargerMode? Mode
		{
		get; init;
		}
	internal bool? Online
		{
		get; init;
		}
	internal double? PowerKilowatts
		{
		get; init;
		}
	internal double? SessionEnergyKilowattHours
		{
		get; init;
		}
	internal double? LifetimeEnergyKilowattHours
		{
		get; init;
		}
	internal double? AvailableCurrentAmps
		{
		get; init;
		}
	internal double? MaximumCurrentAmps
		{
		get; init;
		}
	internal double? CableRatingAmps
		{
		get; init;
		}
	internal bool? KeepCableLocked
		{
		get; init;
		}
	internal bool? CableLocked
		{
		get; init;
		}
	internal int? ErrorCode
		{
		get; init;
		}
	internal DateTimeOffset? ObservedAt
		{
		get; init;
		}
	internal DateTimeOffset? NextScheduledStart
		{
		get; init;
		}
	internal string? ScheduledStartDisplay
		{
		get; init;
		}

	internal bool ModeKnown => Mode.HasValue && Enum.IsDefined (typeof (ChargerMode), Mode.Value);
	internal bool? VehicleConnected => !ModeKnown ? null : Mode switch
		{
			ChargerMode.Disconnected => false,
			ChargerMode.Error or ChargerMode.Offline => null,
			_ => true
			};
	internal bool Charging => Mode == ChargerMode.Charging;
	internal bool CanStart => Online == true && VehicleConnected == true && !Charging && Mode != ChargerMode.Deauthenticating && ErrorCode is null or 0;
	internal bool CanStop => Online == true && VehicleConnected == true && Charging;
	internal string Status => Online != true || Mode == ChargerMode.Offline ? "Offline" : !ModeKnown ? "Status unavailable" : Mode switch
		{
			ChargerMode.Disconnected => "Not connected",
			ChargerMode.Charging => "Charging",
			ChargerMode.Completed => "Not charging",
			ChargerMode.AwaitingAuthentication => "Awaiting authentication",
			ChargerMode.Deauthenticating => "Stopping",
			ChargerMode.Error => "Charger error",
			_ => "Not charging"
			};
	internal string PowerDisplay => Format (PowerKilowatts, "kW");
	internal string SessionEnergyDisplay => Format (SessionEnergyKilowattHours, "kWh");
	internal string TileStatus
		{
		get
			{
			// Home renders a single, narrow subtitle. Keep power visible and leave the full
			// mode description on the details page; the tile icon also identifies the state.
			if (Online != true || Mode == ChargerMode.Offline)
				return "Offline";
			if (!ModeKnown)
				return "Unknown";
			if (Mode == ChargerMode.Error)
				return "Error";
			if (VehicleConnected != true)
				return "No car";
			if (!Charging && NextScheduledStart.HasValue && !string.IsNullOrWhiteSpace (ScheduledStartDisplay))
				return "At " + ScheduledStartDisplay;
			string state = Charging ? "On" : Mode == ChargerMode.Deauthenticating ? "Wait" : "Idle";
			return PowerDisplay.Replace (" ", "") + " " + state;
			}
		}
	internal static string Format (double? value, string units) => !value.HasValue || double.IsNaN (value.Value) || double.IsInfinity (value.Value)
		 ? "--" : value.Value.ToString ("0.0", CultureInfo.InvariantCulture) + " " + units;
	}
