namespace Easee.CrestronDriver;

internal interface IChargerGateway
	{
	Task<ChargerReading> ReadAsync (CancellationToken cancellationToken);
	Task AuthorizeAsync (CancellationToken cancellationToken);
	Task OverrideScheduleAsync (CancellationToken cancellationToken);
	Task StopAsync (CancellationToken cancellationToken);
	Task SetCurrentAsync (int amps, int minutes, CancellationToken cancellationToken);
	Task SetCableLockAsync (bool keepLocked, CancellationToken cancellationToken);
	}

// Each command verifies the selected charger's current state. Writes are never retried automatically.
internal sealed class ChargerCommands (IChargerGateway gateway)
	{
	private readonly SemaphoreSlim _gate = new (1, 1);
	internal async Task<string> StartAsync (CancellationToken cancellationToken = default)
		{
		await _gate.WaitAsync (cancellationToken).ConfigureAwait (false);
		try
			{
			var state = await gateway.ReadAsync (cancellationToken).ConfigureAwait (false);
			if (!state.CanStart)
				return state.Charging ? "Already charging" : "Charge unavailable";
			await gateway.AuthorizeAsync (cancellationToken).ConfigureAwait (false);
			// Stop/pause may have deauthorized the session. Authorize first, then ignore its schedule.
			await gateway.OverrideScheduleAsync (cancellationToken).ConfigureAwait (false);
			return "Immediate charging requested";
			}
		finally { _gate.Release (); }
		}
	internal async Task<string> StopAsync (CancellationToken cancellationToken = default)
		{
		await _gate.WaitAsync (cancellationToken).ConfigureAwait (false);
		try
			{
			var state = await gateway.ReadAsync (cancellationToken).ConfigureAwait (false);
			if (state.Online != true || state.VehicleConnected != true)
				return "Stop unavailable";
			// Also allow deauthorizing an idle connected session, e.g. while awaiting its schedule.
			await gateway.StopAsync (cancellationToken).ConfigureAwait (false);
			return "Charging stop requested";
			}
		finally { _gate.Release (); }
		}
	internal async Task<string> ToggleAsync (CancellationToken cancellationToken = default)
		{
		await _gate.WaitAsync (cancellationToken).ConfigureAwait (false);
		try
			{
			var state = await gateway.ReadAsync (cancellationToken).ConfigureAwait (false);
			if (state.CanStop)
				{
				await gateway.StopAsync (cancellationToken).ConfigureAwait (false);
				return "Charging stop requested";
				}
			if (!state.CanStart)
				return "Charge unavailable";
			await gateway.AuthorizeAsync (cancellationToken).ConfigureAwait (false);
			await gateway.OverrideScheduleAsync (cancellationToken).ConfigureAwait (false);
			return "Immediate charging requested";
			}
		finally { _gate.Release (); }
		}
	internal async Task<string> SetCurrentAsync (int amps, int minutes, CancellationToken cancellationToken = default)
		{
		if (amps < 0)
			throw new ArgumentOutOfRangeException (nameof (amps));
		if (minutes <= 0)
			throw new ArgumentOutOfRangeException (nameof (minutes), "A positive duration is required for a temporary limit.");
		await _gate.WaitAsync (cancellationToken).ConfigureAwait (false);
		try
			{
			var state = await gateway.ReadAsync (cancellationToken).ConfigureAwait (false);
			if (state.Online != true)
				return "Charger offline";
			if (!state.MaximumCurrentAmps.HasValue)
				return "Charger current limit unavailable";
			var maximum = state.MaximumCurrentAmps.Value;
			if (state.CableRatingAmps is > 0)
				maximum = Math.Min (maximum, state.CableRatingAmps.Value);
			if (amps > maximum)
				throw new ArgumentOutOfRangeException (nameof (amps), "The requested current exceeds the reported charger or cable limit.");
			await gateway.SetCurrentAsync (amps, minutes, cancellationToken).ConfigureAwait (false);
			return "Temporary current limit requested";
			}
		finally { _gate.Release (); }
		}
	internal async Task<string> SetCableLockAsync (bool keepLocked, CancellationToken cancellationToken = default)
		{
		await _gate.WaitAsync (cancellationToken).ConfigureAwait (false);
		try
			{
			var state = await gateway.ReadAsync (cancellationToken).ConfigureAwait (false);
			if (state.Online != true)
				return "Charger offline";
			await gateway.SetCableLockAsync (keepLocked, cancellationToken).ConfigureAwait (false);
			return "Cable lock setting requested";
			}
		finally { _gate.Release (); }
		}
	}