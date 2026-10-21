using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using EaseeChargerClient;

namespace Easee.CrestronDriver.LiveTests;

// Windows preparation only. The processor receives access-only inputs, never this account file or refresh chain.
[TestFixture, Category ("LiveSetup"), NonParallelizable]
public sealed class IndependentSessionTests
	{
	[Test]
	public async Task PrepareAnIndependentDriverSessionFromTheEncryptedAccount ()
		{
		if (Environment.GetEnvironmentVariable ("EASEE_DRIVER_PREPARE_SESSION") != "1")
			Assert.Ignore ("Independent account preparation is disabled.");
		string directory = Environment.GetEnvironmentVariable ("EASEE_DRIVER_LIVE_DIRECTORY") ?? throw new InvalidOperationException ("A private preparation directory is required.");
		var clear = ProtectedData.Unprotect (File.ReadAllBytes (Path.Combine (directory, "Account.dpapi")), null, DataProtectionScope.CurrentUser);
		Account account;
		try
			{
			account = JsonSerializer.Deserialize<Account> (Encoding.UTF8.GetString (clear), LiveInputs.Json) ?? throw new InvalidDataException ("Missing encrypted account.");
			}
		finally { Array.Clear (clear, 0, clear.Length); }
		using var authentication = new EaseeClient ();
		using var session = new RecoveringTokenProvider (authentication, account.UserName, account.Password, (token, _) =>
			 {
				 byte[] bytes = Encoding.UTF8.GetBytes (JsonSerializer.Serialize (token));
				 try
					 {
					 File.WriteAllBytes (Path.Combine (directory, "Session.dpapi"), ProtectedData.Protect (bytes, null, DataProtectionScope.CurrentUser));
					 }
				 finally { Array.Clear (bytes, 0, bytes.Length); }
				 return Task.CompletedTask;
			 }, null);
		string access = await session.GetAccessTokenAsync ();
		byte[] saved = ProtectedData.Unprotect (File.ReadAllBytes (Path.Combine (directory, "Session.dpapi")), null, DataProtectionScope.CurrentUser);
		EaseeToken accepted;
		try
			{
			accepted = JsonSerializer.Deserialize<EaseeToken> (Encoding.UTF8.GetString (saved))!;
			}
		finally { Array.Clear (saved, 0, saved.Length); }
		var inputs = new LiveInputs { AccessToken = access, ExpiresAt = accepted.ExpiresAt, SiteId = account.SiteId, ChargerId = account.ChargerId };
		using var client = new EaseeClient (new EaseeClientOptions { TokenProvider = inputs });
		var chargers = await client.SiteGetChargersAsync (account.SiteId);
		Assert.That (chargers.Value?.Any (c => string.Equals (c.Id, account.ChargerId, StringComparison.OrdinalIgnoreCase)), Is.True, "The saved selection is unavailable to the newly signed-in account.");
		File.WriteAllText (Path.Combine (directory, "LiveTestSettings.json"), JsonSerializer.Serialize (inputs), new UTF8Encoding (false));
		TestContext.Out.WriteLine ("Independent driver sign-in and selected-charger access verified. No client refresh token was reused.");
		}
	private sealed class Account
		{
		public string UserName { get; set; } = "";
		public string Password { get; set; } = "";
		public string SiteId { get; set; } = "";
		public string ChargerId { get; set; } = "";
		}
	}