using System.Globalization;

using EaseeChargerClient.Models;

using TimeZoneConverter;

namespace Easee.CrestronDriver;

internal sealed record ScheduleStart (DateTimeOffset? Instant, string Display, string Detail);
internal static class SchedulePlanner
	{
	private sealed record Window (DateTime Start, DateTime Stop);
	internal static ScheduleStart Next (Scheduled_charging_GetScheduleStateResponse? schedule, DateTimeOffset now)
		{
		if (schedule is null)
			return new (null, "", "Schedule unavailable");
		if (schedule.Enabled == "none")
			return new (null, "", "No charging schedule");
		try
			{
			string? zoneId = schedule.Enabled switch
				{
					"delayed" => schedule.Delayed?.Timezone,
					"daily" => schedule.Daily?.Timezone,
					"weekly" => schedule.Weekly?.Timezone,
					"offPeak" or "offpeak" => schedule.OffPeak?.Timezone,
					"tariff" => schedule.Tariff?.Timezone,
					_ => null
					};
			if (string.IsNullOrWhiteSpace (zoneId))
				return new (null, "", "Schedule time unavailable");
			var zone = TZConvert.GetTimeZoneInfo (zoneId!);
			var localNow = TimeZoneInfo.ConvertTime (now, zone).DateTime;
			if (schedule.Enabled == "tariff")
				{
				var periods = schedule.Tariff?.Periods;
				if (periods is null)
					return new (null, "", "Schedule time unavailable");
				if (periods.Any (p => p.MaximumAmps > 0 && p.Start <= now && now < p.Stop))
					return PermitsNow ();
				var next = periods.Where (p => p.MaximumAmps > 0 && p.Start > now).OrderBy (p => p.Start).FirstOrDefault ();
				return next is null ? new (null, "", "No future charging period") : Describe (next.Start, zone, zoneId!, localNow);
				}
			var windows = new List<Window> ();
			if (schedule.Enabled == "delayed" && schedule.Delayed is { } delayed)
				{
				if (delayed.MaximumAmps <= 0)
					return new (null, "", "Schedule permits no current");
				if (delayed.Elapsed)
					return PermitsNow ();
				var time = Time (delayed.StartTime);
				var start = localNow.Date + time;
				if (start < localNow)
					start = start.AddDays (1);
				return Describe (Instant (start, zone), zone, zoneId!, localNow);
				}
			if (schedule.Enabled == "daily" && schedule.Daily?.Periods is { } daily)
				{
				foreach (var period in daily.Where (p => p.MaximumAmps > 0))
					for (int offset = -1; offset <= 7; offset++)
						{
						var start = localNow.Date.AddDays (offset) + Time (period.StartTime);
						var stop = localNow.Date.AddDays (offset) + Time (period.StopTime);
						if (stop <= start)
							stop = stop.AddDays (1);
						windows.Add (new (start, stop));
						}
				}
			else if (schedule.Enabled == "weekly" && schedule.Weekly?.Periods is { } weekly)
				foreach (var period in weekly.Where (p => p.MaximumAmps > 0))
					AddWeekly (windows, localNow, (int)period.StartDay, period.StartTime, (int)period.StopDay, period.StopTime);
			else if (schedule.Enabled is "offPeak" or "offpeak" && schedule.OffPeak is { } offPeak)
				{
				if (offPeak.RandomizedDelay?.Enabled == true && offPeak.RandomizedDelay.ActualSeconds is null)
					return new (null, "", "Schedule start awaits randomized delay");
				var delay = offPeak.RandomizedDelay?.Enabled == true ? offPeak.RandomizedDelay.ActualSeconds ?? 0 : 0;
				if (delay < 0)
					return new (null, "", "Schedule time unavailable");
				foreach (var period in (offPeak.Periods ?? []).Where (p => p.MaximumAmps > 0))
					AddWeekly (windows, localNow, (int)period.StartDay, period.StartTime, (int)period.StopDay, period.StopTime, delay);
				}
			else
				return new (null, "", "Schedule time unavailable");

			if (windows.Any (w => w.Start <= localNow && localNow < w.Stop))
				return PermitsNow ();
			var candidate = windows.Where (w => w.Start > localNow && w.Stop > w.Start).OrderBy (w => w.Start).FirstOrDefault ();
			return candidate is null ? new (null, "", "No future charging period") : Describe (Instant (candidate.Start, zone), zone, zoneId!, localNow);
			}
		catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException or FormatException or OverflowException)
			{
			// Do not silently substitute the processor's time zone or invent DST resolution.
			return new (null, "", "Schedule time unavailable");
			}
		}
	private static void AddWeekly (List<Window> windows, DateTime localNow, int day, string startText, int stopDay, string stopText, int delaySeconds = 0)
		{
		if (day is < 0 or > 6 || stopDay is < 0 or > 6)
			throw new ArgumentException ("Unknown schedule day.");
		for (int offset = -7; offset <= 14; offset++)
			{
			var date = localNow.Date.AddDays (offset);
			if ((int)date.DayOfWeek != day)
				continue;
			var start = date + Time (startText);
			var stop = date.AddDays ((stopDay - day + 7) % 7) + Time (stopText);
			if (stop <= start)
				stop = stop.AddDays (7);
			windows.Add (new (start.AddSeconds (delaySeconds), stop));
			}
		}
	private static TimeSpan Time (string value)
		{
		if (!TimeSpan.TryParseExact (value, [@"hh\:mm", @"hh\:mm\:ss", @"hh\:mm\:ss\.FFFFFFF"], CultureInfo.InvariantCulture, out var time) || time < TimeSpan.Zero || time >= TimeSpan.FromDays (1))
			throw new FormatException ("Invalid schedule time.");
		return time;
		}
	private static DateTimeOffset Instant (DateTime local, TimeZoneInfo zone)
		{
		local = DateTime.SpecifyKind (local, DateTimeKind.Unspecified);
		if (zone.IsInvalidTime (local) || zone.IsAmbiguousTime (local))
			throw new ArgumentException ("The schedule boundary needs a DST resolution not published by Easee.");
		return new DateTimeOffset (local, zone.GetUtcOffset (local));
		}
	private static ScheduleStart Describe (DateTimeOffset instant, TimeZoneInfo zone, string zoneId, DateTime localNow)
		{
		var local = TimeZoneInfo.ConvertTime (instant, zone);
		var text = local.ToString (local.Date == localNow.Date ? "HH:mm" : "ddd HH:mm", CultureInfo.InvariantCulture);
		return new (instant, text, text + " (" + zoneId + ")");
		}
	private static ScheduleStart PermitsNow () => new (null, "", "Schedule currently permits charging");
	}