using System.Text.Json;

using EaseeChargerClient;

namespace Easee.CrestronDriver.LiveTests;

internal sealed class LiveInputs : IEaseeTokenProvider
	{
	public string AccessToken { get; set; } = "";
	public DateTimeOffset ExpiresAt
		{
		get; set;
		}
	public string SiteId { get; set; } = "";
	public string ChargerId { get; set; } = "";
	internal static readonly JsonSerializerOptions Json = new () { PropertyNameCaseInsensitive = true };
	internal static string Directory => TestContext.Parameters.Get ("TestDataDirectory", Environment.GetEnvironmentVariable ("EASEE_DRIVER_LIVE_DIRECTORY") ?? AppDomain.CurrentDomain.BaseDirectory);
	internal static LiveInputs Load ()
		{
		if (!TestContext.Parameters.Get ("EnableLiveTests", Environment.GetEnvironmentVariable ("EASEE_DRIVER_ENABLE_LIVE") == "1"))
			Assert.Ignore ("Live tests are disabled.");
		var inputs = JsonSerializer.Deserialize<LiveInputs> (File.ReadAllText (Path.Combine (Directory, "LiveTestSettings.json")), Json) ?? throw new InvalidDataException ("Missing private live-test inputs.");
		if (string.IsNullOrWhiteSpace (inputs.SiteId) || string.IsNullOrWhiteSpace (inputs.ChargerId))
			throw new InvalidDataException ("The explicit site and charger selection is missing.");
		return inputs;
		}
	public Task<string> GetAccessTokenAsync (CancellationToken cancellationToken = default)
		{
		cancellationToken.ThrowIfCancellationRequested ();
		if (string.IsNullOrWhiteSpace (AccessToken) || ExpiresAt <= DateTimeOffset.UtcNow)
			throw new InvalidOperationException ("Prepare fresh access-only live-test inputs.");
		return Task.FromResult (AccessToken);
		}
	}