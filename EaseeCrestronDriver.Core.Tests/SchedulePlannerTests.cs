using EaseeChargerClient.Models;

namespace Easee.CrestronDriver.Tests;

[TestFixture]
internal sealed class SchedulePlannerTests
	{
	[Test]
	public void DailyStartUsesSiteZoneAndNotProcessorZone ()
		{
		var schedule = Daily ("01:30", "03:00");
		var next = SchedulePlanner.Next (schedule, DateTimeOffset.Parse ("2026-07-20T23:00:00Z"));
		Assert.That (next.Instant, Is.EqualTo (DateTimeOffset.Parse ("2026-07-21T00:30:00Z")));
		Assert.That (next.Detail, Does.Contain ("Europe/London"));
		}
	[Test]
	public void OvernightDailyWindowAlreadyPermitsCharging ()
		{
		var next = SchedulePlanner.Next (Daily ("23:00", "02:00"), DateTimeOffset.Parse ("2026-10-21T00:00:00Z"));
		Assert.That (next.Instant, Is.Null);
		Assert.That (next.Detail, Is.EqualTo ("Schedule currently permits charging"));
		}
	[TestCase ("2026-10-25T00:00:00Z")] // Repeated 01:30 local time at autumn transition.
	[TestCase ("2026-03-29T00:00:00Z")] // Missing 01:30 local time at spring transition.
	public void AmbiguousOrMissingDstBoundaryDoesNotInventAStart (string now)
		{
		var result = SchedulePlanner.Next (Daily ("01:30", "03:00"), DateTimeOffset.Parse (now));
		Assert.That (result.Instant, Is.Null);
		Assert.That (result.Detail, Is.EqualTo ("Schedule time unavailable"));
		}
	[Test]
	public void WeeklySundayToMondayPeriodIsRecognizedAcrossWeekBoundary ()
		{
		var schedule = new Scheduled_charging_GetScheduleStateResponse
			{
			Enabled = "weekly",
			Weekly = new ()
				{
				Timezone = "Europe/London",
				Periods = [new () { StartDay = Scheduled_charging_DayOfWeek.Sunday, StartTime = "23:00", StopDay = Scheduled_charging_DayOfWeek.Monday, StopTime = "03:00", MaximumAmps = 16 }]
				}
			};
		Assert.That (SchedulePlanner.Next (schedule, DateTimeOffset.Parse ("2026-10-19T00:30:00Z")).Detail, Is.EqualTo ("Schedule currently permits charging"));
		}
	[Test]
	public void OffPeakUsesPublishedActualRandomOffset ()
		{
		var schedule = new Scheduled_charging_GetScheduleStateResponse
			{
			Enabled = "offPeak",
			OffPeak = new ()
				{
				Timezone = "Europe/London",
				RandomizedDelay = new ()
					{
					Enabled = true,
					ActualSeconds = 300
					},
				Periods = [new () { StartDay = Scheduled_charging_DayOfWeek.Wednesday, StartTime = "23:00", StopDay = Scheduled_charging_DayOfWeek.Thursday, StopTime = "03:00", MaximumAmps = 16 }]
				}
			};
		var now = DateTimeOffset.Parse ("2026-10-21T20:00:00Z");
		Assert.That (SchedulePlanner.Next (schedule, now).Instant, Is.EqualTo (DateTimeOffset.Parse ("2026-10-21T22:05:00Z")));
		schedule.OffPeak.RandomizedDelay.ActualSeconds = null;
		Assert.That (SchedulePlanner.Next (schedule, now).Instant, Is.Null);
		}
	[Test]
	public void TariffUsesAbsoluteInstantsAndSkipsZeroCurrentPeriods ()
		{
		var schedule = new Scheduled_charging_GetScheduleStateResponse
			{
			Enabled = "tariff",
			Tariff = new ()
				{
				Timezone = "Europe/London",
				Periods = [new() { Start = DateTimeOffset.Parse("2026-10-21T21:00:00Z"), Stop = DateTimeOffset.Parse("2026-10-21T22:00:00Z"), MaximumAmps = 0 },
					 new() { Start = DateTimeOffset.Parse("2026-10-21T23:00:00Z"), Stop = DateTimeOffset.Parse("2026-10-22T01:00:00Z"), MaximumAmps = 16 }]
				}
			};
		Assert.That (SchedulePlanner.Next (schedule, DateTimeOffset.Parse ("2026-10-21T20:00:00Z")).Instant, Is.EqualTo (DateTimeOffset.Parse ("2026-10-21T23:00:00Z")));
		}
	[Test]
	public void ElapsedDelayedScheduleDoesNotAdvertiseTomorrowAsItsStart ()
		{
		var schedule = new Scheduled_charging_GetScheduleStateResponse { Enabled = "delayed", Delayed = new () { Timezone = "Europe/London", StartTime = "01:30", Elapsed = true, MaximumAmps = 16 } };
		Assert.That (SchedulePlanner.Next (schedule, DateTimeOffset.Parse ("2026-10-21T20:00:00Z")).Detail, Is.EqualTo ("Schedule currently permits charging"));
		}
	[TestCase ("not/a/zone", "01:30")]
	[TestCase ("Europe/London", "garbage")]
	[TestCase ("Europe/London", "25:00")]
	public void UnsupportedOrInvalidInputIsExplicitlyUnavailable (string zone, string start)
		{
		var schedule = Daily (start, "03:00");
		schedule.Daily!.Timezone = zone;
		var result = SchedulePlanner.Next (schedule, DateTimeOffset.Parse ("2026-10-21T20:00:00Z"));
		Assert.That (result.Instant, Is.Null);
		Assert.That (result.Detail, Is.EqualTo ("Schedule time unavailable"));
		}
	private static Scheduled_charging_GetScheduleStateResponse Daily (string start, string stop) => new ()
		{
		Enabled = "daily",
		Daily = new ()
			{
			Timezone = "Europe/London",
			Periods = [new () { StartTime = start, StopTime = stop, MaximumAmps = 16 }]
			}
		};
	}