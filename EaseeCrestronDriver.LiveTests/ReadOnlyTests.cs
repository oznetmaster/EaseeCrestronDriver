using EaseeChargerClient;

namespace Easee.CrestronDriver.LiveTests;

[TestFixture, Category ("Live"), NonParallelizable]
public sealed class ReadOnlyTests
	{
	private LiveInputs _inputs = null!;
	private EaseeClient _client = null!;
	[SetUp]
	public void SetUp ()
		{
		_inputs = LiveInputs.Load ();
		_client = new EaseeClient (new EaseeClientOptions { TokenProvider = _inputs });
		}
	[TearDown] public void TearDown () => _client?.Dispose ();
	[Test]
	public async Task SavedSelectionBelongsToTheAccountAndSite ()
		{
		var sites = await _client.SiteGetAllAsync ();
		Assert.That (sites.Value?.Any (s => s.Id?.ToString (System.Globalization.CultureInfo.InvariantCulture) == _inputs.SiteId), Is.True, "Selected site is unavailable.");
		var chargers = await _client.SiteGetChargersAsync (_inputs.SiteId);
		Assert.That (chargers.Value?.Any (c => string.Equals (c.Id, _inputs.ChargerId, StringComparison.OrdinalIgnoreCase)), Is.True, "Selected charger is unavailable at that site.");
		}
	[Test]
	public async Task DriverGatewayReadsTypedModeConnectivityAndPower ()
		{
		var reading = await new EaseeGateway (_client, _inputs.ChargerId).ReadAsync (CancellationToken.None);
		Assert.That (reading.ModeKnown, Is.True);
		Assert.That (reading.Online, Is.Not.Null);
		Assert.That (reading.PowerKilowatts, Is.Not.Null.And.GreaterThanOrEqualTo (0));
		TestContext.Out.WriteLine ("Observed mode=" + reading.Mode + "; online=" + reading.Online + "; power=" + reading.PowerDisplay + ".");
		}
	[Test]
	public async Task PublishedScheduleCanBeReadAndInterpreted ()
		{
		var response = await _client.GetChargersSchedulesAsync (_inputs.ChargerId);
		Assert.That ((int)response.StatusCode, Is.InRange (200, 299));
		Assert.That (SchedulePlanner.Next (response.Value, DateTimeOffset.UtcNow).Detail, Is.Not.Empty);
		}
	[Test]
	public async Task SignalRCurrentStateReachesTheDriverObservationModel ()
		{
		var observations = new ObservationState (_inputs.ChargerId);
		var modeReceived = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);
		await using var realtime = new EaseeRealtimeClient (_inputs, [_inputs.ChargerId], update =>
			 {
				 if (observations.Apply (update) && observations.Read (DateTimeOffset.UtcNow).ModeKnown)
					 modeReceived.TrySetResult (true);
				 return Task.CompletedTask;
			 });
		using var timeout = new CancellationTokenSource (TimeSpan.FromSeconds (60));
		await realtime.StartAsync (timeout.Token);
		Assert.That (await Task.WhenAny (modeReceived.Task, Task.Delay (TimeSpan.FromSeconds (45), timeout.Token)), Is.SameAs (modeReceived.Task), "No typed selected-charger mode arrived through SignalR.");
		}
	}