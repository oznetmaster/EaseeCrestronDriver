using System.Text.Json;

using EaseeChargerClient;

namespace Easee.CrestronDriver.LiveTests;

[TestFixture, Category ("Live"), Category ("LiveControl"), NonParallelizable]
public sealed class ChargingRequestTests
	{
	[Test]
	public async Task OneImmediateStartAndOneStopAreAcceptedAndStatusRemainsObservable ()
		{
		if (Environment.GetEnvironmentVariable ("EASEE_DRIVER_ALLOW_START_STOP") != "1")
			Assert.Ignore ("A separately authorized start/stop run is required.");
		var inputs = LiveInputs.Load ();
		string journal = Path.Combine (LiveInputs.Directory, "ChargingRequestIntent.json");
		using (var intent = new FileStream (journal, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
			{
			byte[] bytes = System.Text.Encoding.UTF8.GetBytes (JsonSerializer.Serialize (new
				{
				Phase = "Intent",
				Utc = DateTimeOffset.UtcNow
				}));
			intent.Write (bytes, 0, bytes.Length);
			intent.Flush (true);
			}
		using var client = new EaseeClient (new EaseeClientOptions { TokenProvider = inputs });
		var gateway = new EaseeGateway (client, inputs.ChargerId);
		var commands = new ChargerCommands (gateway);
		var before = await gateway.ReadAsync (CancellationToken.None);
		Assert.That (before.Online == true && before.VehicleConnected == true && before.CanStart, Is.True, "The selected car must be connected, online and idle for this request test.");
		string start = "Not attempted", stop = "Not attempted";
		bool attempted = false;
		try
			{
			attempted = true;
			start = await commands.StartAsync ();
			Assert.That (start, Is.EqualTo ("Immediate charging requested"));
			await Task.Delay (TimeSpan.FromSeconds (10));
			var afterStart = await gateway.ReadAsync (CancellationToken.None);
			TestContext.Out.WriteLine ("Start request accepted; observed mode=" + afterStart.Mode + "; power=" + afterStart.PowerDisplay + ".");
			Assert.That (afterStart.ModeKnown && afterStart.Mode != ChargerMode.Error, Is.True);
			}
		finally
			{
			if (attempted)
				{
				stop = await commands.StopAsync ();
				File.WriteAllText (journal, JsonSerializer.Serialize (new
					{
					Phase = "StopResponse",
					Start = start,
					Stop = stop,
					Utc = DateTimeOffset.UtcNow
					}));
				}
			}
		Assert.That (stop, Is.EqualTo ("Charging stop requested"));
		await Task.Delay (TimeSpan.FromSeconds (10));
		var afterStop = await gateway.ReadAsync (CancellationToken.None);
		Assert.That (afterStop.ModeKnown && afterStop.Mode != ChargerMode.Error && !afterStop.Charging, Is.True, "The charger did not report a noncharging mode after Stop.");
		File.WriteAllText (journal, JsonSerializer.Serialize (new
			{
			Phase = "NonchargingVerified",
			Mode = afterStop.Mode.ToString (),
			Utc = DateTimeOffset.UtcNow
			}));
		TestContext.Out.WriteLine ("Stop request accepted and noncharging verified. This request test does not prove energy delivery to a full vehicle.");
		}
	}