using System.Globalization;

using EaseeChargerClient;
using EaseeChargerClient.Models;

namespace Easee.CrestronDriver;

// Only the published primitive observations used by this driver are consumed.
// Each observation has its own ordering; a newer power sample must not discard an older mode sample.
internal sealed class ObservationState (string serialNumber)
	{
	private readonly Dictionary<int, Current_Device_State_API_Observation> _values = new ();
	private readonly object _gate = new ();
	internal const string RequestedIds = "30,47,48,103,104,109,119,120,121,124,250";

	internal bool Apply (IEnumerable<Current_Device_State_API_Observation> observations)
		{
		bool changed = false;
		lock (_gate)
			foreach (var observation in observations)
				{
				if (observation.Id is not int id || observation.Timestamp is not DateTimeOffset timestamp || timestamp == default || !IsUsable (observation, id))
					continue;
				if (_values.TryGetValue (id, out var old) && old.Timestamp >= timestamp)
					continue;
				_values[id] = observation;
				changed = true;
				}
		return changed;
		}

	internal bool Apply (EaseeProductUpdate update)
		{
		if (!string.Equals (update.DeviceId, serialNumber, StringComparison.OrdinalIgnoreCase) || update.Timestamp == default)
			return false;
		Current_Device_State_API_Observation? observation = update.DataType switch
			{
				2 when bool.TryParse (update.Value, out var b) => new EaseeBooleanObservation { Value = b },
				2 when update.Value is "0" or "1" => new EaseeBooleanObservation { Value = update.Value == "1" },
				3 when double.TryParse (update.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && !double.IsNaN (d) && !double.IsInfinity (d) => new EaseeDoubleObservation { Value = d },
				4 when long.TryParse (update.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) => new EaseeIntegerObservation { Value = n },
				_ => null
				};
		if (observation is null)
			return false;
		observation.Id = update.ObservationId;
		observation.Timestamp = update.Timestamp;
		return Apply ([observation]);
		}

	private static bool IsUsable (Current_Device_State_API_Observation observation, int id) => id switch
		{
			30 or 103 or 250 => observation is EaseeBooleanObservation { Value: not null },
			109 or 119 => observation is EaseeIntegerObservation { Value: not null },
			47 or 48 or 104 or 120 or 121 or 124 => observation switch
				{
					EaseeDoubleObservation { Value: double n } => !double.IsNaN (n) && !double.IsInfinity (n) && n >= 0,
					EaseeIntegerObservation { Value: long n } => n >= 0,
					_ => false
					},
			_ => false
			};

	internal ChargerReading Read (DateTimeOffset receivedAt)
		{
		lock (_gate)
			{
			return new ChargerReading
				{
				Mode = Integer (109) is long mode && mode >= int.MinValue && mode <= int.MaxValue ? (ChargerMode)(int)mode : null,
				Online = Integer (109) == 0 ? false : Boolean (250),
				PowerKilowatts = Number (120),
				SessionEnergyKilowattHours = Number (121),
				LifetimeEnergyKilowattHours = Number (124),
				AvailableCurrentAmps = Number (48),
				MaximumCurrentAmps = Number (47),
				CableRatingAmps = Number (104),
				CableLocked = Boolean (103),
				KeepCableLocked = Boolean (30),
				ErrorCode = Integer (119) is long error && error >= int.MinValue && error <= int.MaxValue ? (int)error : null,
				ObservedAt = receivedAt
				};
			}
		}
	private bool? Boolean (int id) => _values.TryGetValue (id, out var item) && item is EaseeBooleanObservation value ? value.Value : null;
	private long? Integer (int id) => _values.TryGetValue (id, out var item) && item is EaseeIntegerObservation value ? value.Value : null;
	private double? Number (int id)
		{
		double? number = _values.TryGetValue (id, out var item) ? item switch
			{
				EaseeDoubleObservation value => value.Value,
				EaseeIntegerObservation value => value.Value,
				_ => null
				} : null;
		return number.HasValue && !double.IsNaN (number.Value) && !double.IsInfinity (number.Value) && number.Value >= 0 ? number : null;
		}
	}