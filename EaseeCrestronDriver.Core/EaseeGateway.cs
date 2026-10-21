using EaseeChargerClient;
using EaseeChargerClient.Models;

namespace Easee.CrestronDriver;

internal sealed class EaseeGateway (EaseeClient client, string serialNumber) : IChargerGateway
	{
	internal ObservationState Observations { get; } = new (serialNumber);
	public async Task<ChargerReading> ReadAsync (CancellationToken cancellationToken)
		{
		var response = await client.GetobservationsAsync (serialNumber, ObservationState.RequestedIds, cancellationToken: cancellationToken).ConfigureAwait (false);
		if (response.Value?.Observations is not { } observations)
			throw new InvalidDataException ("No charger observations were returned.");
		if (!observations.Any (o => o.Id == 109 && o is EaseeIntegerObservation { Value: not null }) ||
			 !observations.Any (o => o.Id == 250 && o is EaseeBooleanObservation { Value: not null }))
			throw new InvalidDataException ("Required charger mode and cloud connectivity observations were not returned.");
		Observations.Apply (observations);
		return Observations.Read (DateTimeOffset.UtcNow);
		}
	public async Task AuthorizeAsync (CancellationToken cancellationToken) => await client.AuthorizeChargingStartChargingAsync (serialNumber, cancellationToken: cancellationToken).ConfigureAwait (false);
	public async Task OverrideScheduleAsync (CancellationToken cancellationToken) => await client.ChargerOverrideScheduleAsync (serialNumber, cancellationToken: cancellationToken).ConfigureAwait (false);
	public async Task StopAsync (CancellationToken cancellationToken) => await client.ChargerStopChargingAsync (serialNumber, cancellationToken: cancellationToken).ConfigureAwait (false);
	public async Task SetCurrentAsync (int amps, int minutes, CancellationToken cancellationToken) => await client.ChargerSetDynamicChargerCurrentAsync (serialNumber, new Easee_Command_API_DynamicChargerCurrentDTO { Amps = amps, Minutes = minutes }, cancellationToken: cancellationToken).ConfigureAwait (false);
	public async Task SetCableLockAsync (bool keepLocked, CancellationToken cancellationToken) => await client.ChargerSetCableLockStateAsync (serialNumber, new Easee_Command_API_LockStateDTO { State = keepLocked }, cancellationToken: cancellationToken).ConfigureAwait (false);
	}