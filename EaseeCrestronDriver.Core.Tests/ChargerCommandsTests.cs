using EaseeChargerClient.Models;

namespace Easee.CrestronDriver.Tests;

[TestFixture]
internal sealed class ChargerCommandsTests
	{
	[Test]
	public async Task ImmediateStartAuthorizesThenOverridesWithoutInventingChargingState ()
		{
		var gateway = new FakeGateway ();
		var result = await new ChargerCommands (gateway).StartAsync ();
		Assert.That (gateway.Calls, Is.EqualTo (new[] { "read", "authorize", "override" }));
		Assert.That (result, Is.EqualTo ("Immediate charging requested"));
		Assert.That (gateway.State.Charging, Is.False);
		}
	[TestCase (ChargerMode.Disconnected)]
	[TestCase (ChargerMode.Error)]
	[TestCase ((ChargerMode)999)]
	public async Task CannotStartWithoutConfirmedConnectedCar (ChargerMode mode)
		{
		var gateway = new FakeGateway { State = new ChargerReading { Online = true, Mode = mode } };
		await new ChargerCommands (gateway).StartAsync ();
		Assert.That (gateway.Calls, Is.EqualTo (new[] { "read" }));
		}
	[Test]
	public async Task TileActionReadsAgainInsteadOfTogglingStaleDisplayedState ()
		{
		var gateway = new FakeGateway { State = new ChargerReading { Online = true, Mode = ChargerMode.Charging } };
		await new ChargerCommands (gateway).ToggleAsync ();
		Assert.That (gateway.Calls, Is.EqualTo (new[] { "read", "stop" }));
		}
	[Test]
	public async Task FailedAuthorizeIsNotRetriedAndDoesNotOverrideSchedule ()
		{
		var gateway = new FakeGateway { FailAuthorize = true };
		await Assert.ThrowsAsync<IOException> (async () => await new ChargerCommands (gateway).StartAsync ());
		Assert.That (gateway.Calls, Is.EqualTo (new[] { "read", "authorize" }));
		}
	[TestCase (33, 10)]
	[TestCase (-1, 10)]
	[TestCase (10, 0)]
	[TestCase (10, -1)]
	public async Task InvalidTemporaryCurrentCannotReachCharger (int amps, int minutes)
		{
		var gateway = new FakeGateway ();
		await Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await new ChargerCommands (gateway).SetCurrentAsync (amps, minutes));
		Assert.That (gateway.Calls, Does.Not.Contain ("current"));
		}
	[Test]
	public async Task CurrentUsesAnExplicitExpiryAndDoesNotChangeInstallationLimits ()
		{
		var gateway = new FakeGateway ();
		await new ChargerCommands (gateway).SetCurrentAsync (10, 4);
		Assert.That (gateway.Calls, Is.EqualTo (new[] { "read", "current" }));
		Assert.That (gateway.CurrentRequest, Is.EqualTo ((10, 4)));
		}
	[Test]
	public async Task CableRatingBoundsTemporaryCurrent ()
		{
		var gateway = new FakeGateway ();
		gateway.State = gateway.State with
			{
			CableRatingAmps = 16
			};
		await Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await new ChargerCommands (gateway).SetCurrentAsync (20, 5));
		Assert.That (gateway.Calls, Is.EqualTo (new[] { "read" }));
		}
	private sealed class FakeGateway : IChargerGateway
		{
		internal ChargerReading State = new () { Online = true, Mode = ChargerMode.AwaitingStart, MaximumCurrentAmps = 32 };
		internal readonly List<string> Calls = [];
		internal bool FailAuthorize;
		internal (int, int)? CurrentRequest;
		public Task<ChargerReading> ReadAsync (CancellationToken token)
			{
			Calls.Add ("read");
			return Task.FromResult (State);
			}
		public Task AuthorizeAsync (CancellationToken token)
			{
			Calls.Add ("authorize");
			return FailAuthorize ? Task.FromException (new IOException ("Ambiguous response")) : Task.CompletedTask;
			}
		public Task OverrideScheduleAsync (CancellationToken token)
			{
			Calls.Add ("override");
			return Task.CompletedTask;
			}
		public Task StopAsync (CancellationToken token)
			{
			Calls.Add ("stop");
			return Task.CompletedTask;
			}
		public Task SetCurrentAsync (int amps, int minutes, CancellationToken token)
			{
			Calls.Add ("current");
			CurrentRequest = (amps, minutes);
			return Task.CompletedTask;
			}
		public Task SetCableLockAsync (bool keepLocked, CancellationToken token)
			{
			Calls.Add ("lock");
			return Task.CompletedTask;
			}
		}
	}