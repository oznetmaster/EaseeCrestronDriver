using EaseeChargerClient;
using EaseeChargerClient.Models;

namespace Easee.CrestronDriver.Tests;

[TestFixture]
internal sealed class ObservationStateTests
	{
	private static readonly DateTimeOffset Now = DateTimeOffset.Parse ("2026-10-21T10:00:00Z");
	[Test]
	public void OlderRestResultCannotOverwriteNewerPushAndOrderingIsPerObservation ()
		{
		var state = new ObservationState ("ABC123");
		state.Apply (new EaseeProductUpdate { DeviceId = "ABC123", ObservationId = 120, DataType = 3, Value = "7.2", Timestamp = Now });
		state.Apply ([new EaseeDoubleObservation { Id = 120, Timestamp = Now.AddMinutes (-1), Value = 0 }, new EaseeIntegerObservation { Id = 109, Timestamp = Now.AddDays (-1), Value = 3 }]);
		var reading = state.Read (Now);
		Assert.That (reading.PowerKilowatts, Is.EqualTo (7.2));
		Assert.That (reading.Charging, Is.True);
		}
	[TestCase ("OTHER", 3, "7.2")]
	[TestCase ("ABC123", 3, "NaN")]
	[TestCase ("ABC123", 3, "Infinity")]
	[TestCase ("ABC123", 3, "7,2")]
	[TestCase ("ABC123", 6, "7.2")]
	public void UnrelatedOrMalformedPushCannotInventPower (string serial, int type, string value)
		{
		var state = new ObservationState ("ABC123");
		state.Apply (new EaseeProductUpdate { DeviceId = serial, ObservationId = 120, DataType = type, Value = value, Timestamp = Now });
		Assert.That (state.Read (Now).PowerKilowatts, Is.Null);
		}
	[Test]
	public void OnlyNewUsableObservationsRequestPublication ()
		{
		var state = new ObservationState ("ABC123");
		var update = new EaseeProductUpdate { DeviceId = "ABC123", ObservationId = 120, DataType = 3, Value = "7.2", Timestamp = Now };
		Assert.That (state.Apply (update), Is.True);
		Assert.That (state.Apply (update), Is.False);
		Assert.That (state.Apply (new EaseeProductUpdate { DeviceId = "ABC123", ObservationId = 120, DataType = 2, Value = "true", Timestamp = Now.AddMinutes (1) }), Is.False);
		Assert.That (state.Apply (new EaseeProductUpdate { DeviceId = "ABC123", ObservationId = 999, DataType = 4, Value = "1", Timestamp = Now }), Is.False);
		Assert.That (state.Read (Now).PowerKilowatts, Is.EqualTo (7.2));
		}
	[Test]
	public void MissingTelemetryRemainsUnknown ()
		{
		var reading = new ObservationState ("ABC123").Read (Now);
		Assert.That (reading.Mode, Is.Null);
		Assert.That (reading.VehicleConnected, Is.Null);
		Assert.That (reading.CanStart, Is.False);
		Assert.That (reading.PowerDisplay, Is.EqualTo ("--"));
		}
	[Test]
	public void PhysicalLockAndPermanentSettingAreDifferentValues ()
		{
		var state = new ObservationState ("ABC123");
		state.Apply ([new EaseeBooleanObservation { Id = 30, Timestamp = Now, Value = false }, new EaseeBooleanObservation { Id = 103, Timestamp = Now, Value = true }]);
		var reading = state.Read (Now);
		Assert.That (reading.KeepCableLocked, Is.False);
		Assert.That (reading.CableLocked, Is.True);
		}
	}