using EaseeChargerClient.Models;

namespace Easee.CrestronDriver.Tests;

[TestFixture]
internal sealed class ChargerReadingTests
	{
	[TestCase (ChargerMode.Disconnected, false, false, false)]
	[TestCase (ChargerMode.AwaitingStart, true, true, false)]
	[TestCase (ChargerMode.ReadyToCharge, true, true, false)]
	[TestCase (ChargerMode.Charging, true, false, true)]
	[TestCase (ChargerMode.Completed, true, true, false)]
	public void ConfirmedModeControlsConnectionAndActions (ChargerMode mode, bool connected, bool start, bool stop)
		{
		var reading = new ChargerReading { Mode = mode, Online = true };
		Assert.That (reading.VehicleConnected, Is.EqualTo (connected));
		Assert.That (reading.CanStart, Is.EqualTo (start));
		Assert.That (reading.CanStop, Is.EqualTo (stop));
		}
	[TestCase (null)]
	[TestCase ((ChargerMode)0)]
	[TestCase ((ChargerMode)999)]
	[TestCase (ChargerMode.Error)]
	public void UnknownOrErrorStateDoesNotInventVehiclePresence (ChargerMode? mode)
		{
		var reading = new ChargerReading { Mode = mode, Online = true };
		Assert.That (reading.VehicleConnected, Is.Null);
		Assert.That (reading.CanStart || reading.CanStop, Is.False);
		}
	[TestCase (false)]
	[TestCase (null)]
	public void UnavailableCloudStatePreventsControl (bool? online)
		{
		var reading = new ChargerReading { Mode = ChargerMode.Charging, Online = online };
		Assert.That (reading.CanStart || reading.CanStop, Is.False);
		Assert.That (reading.TileStatus, Is.EqualTo ("Offline"));
		}
	[Test]
	public void IdleConnectedCarShowsVerifiedScheduleTime ()
		{
		var reading = new ChargerReading { Mode = ChargerMode.AwaitingStart, Online = true, NextScheduledStart = DateTimeOffset.Parse ("2026-10-22T01:30:00Z"), ScheduledStartDisplay = "01:30" };
		Assert.That (reading.TileStatus, Is.EqualTo ("At 01:30"));
		Assert.That ((reading with
			{
			Mode = ChargerMode.Disconnected
			}).TileStatus, Is.EqualTo ("No car"));
		Assert.That ((reading with
			{
			Mode = ChargerMode.Charging,
			PowerKilowatts = 7.2
			}).TileStatus, Is.EqualTo ("7.2kW On"));
		}
	[TestCase (ChargerMode.Completed, "Not charging")]
	[TestCase (ChargerMode.AwaitingAuthentication, "Awaiting authentication")]
	public void IdleConnectedCarShowsCompactPowerAndStateWithoutLosingTheFullStatus (ChargerMode mode, string detail)
		{
		var reading = new ChargerReading { Mode = mode, Online = true, PowerKilowatts = 0 };
		Assert.That (reading.TileStatus, Is.EqualTo ("0.0kW Idle"));
		Assert.That (reading.Status, Is.EqualTo (detail));
		}
	[TestCase (null, "--")]
	[TestCase (0d, "0.0 kW")]
	[TestCase (7.26d, "7.3 kW")]
	[SetCulture ("fr-FR")]
	public void PowerIsAlreadyKilowattsAndPreservesUnknown (double? power, string expected) => Assert.That (new ChargerReading { PowerKilowatts = power }.PowerDisplay, Is.EqualTo (expected));
	}
