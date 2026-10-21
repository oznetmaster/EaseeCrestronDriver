using System.Net;
using System.Net.Http;
using System.Text;

using EaseeChargerClient;

namespace Easee.CrestronDriver.Tests;

[TestFixture]
internal sealed class RecoveringTokenProviderTests
	{
	[Test]
	public async Task RestartUsesValidPersistedAccessWithoutFreshSignIn ()
		{
		using var handler = new Handler ();
		using var http = new HttpClient (handler);
		using var auth = new EaseeClient (httpClient: http);
		using var provider = new RecoveringTokenProvider (auth, "test@example.invalid", "synthetic", (_, _) => Task.CompletedTask, Token (DateTimeOffset.UtcNow.AddHours (1)));
		Assert.That (await provider.GetAccessTokenAsync (), Is.EqualTo ("access-old"));
		Assert.That (handler.Calls, Is.Empty);
		}
	[Test]
	public async Task InterruptedRefreshRequiresFreshLoginAndDoesNotReuseOldRefreshToken ()
		{
		using var handler = new Handler ();
		using var http = new HttpClient (handler);
		using var auth = new EaseeClient (httpClient: http);
		var saved = new List<bool> ();
		var token = Token (DateTimeOffset.UtcNow.AddHours (1));
		token.RequiresReauthentication = true;
		using var provider = new RecoveringTokenProvider (auth, "test@example.invalid", "synthetic", (next, _) => { saved.Add (next.RequiresReauthentication); return Task.CompletedTask; }, token);
		Assert.That (await provider.GetAccessTokenAsync (), Is.EqualTo ("access-new"));
		Assert.That (handler.Calls, Is.EqualTo (new[] { "/api/accounts/login" }));
		Assert.That (saved, Is.EqualTo (new[] { false }));
		}
	[Test]
	public async Task RefreshIsCheckpointedBeforeRequestAndNewPairBeforeUse ()
		{
		var ordering = new List<string> ();
		using var handler = new Handler { OnRequest = () => ordering.Add ("request") };
		using var http = new HttpClient (handler);
		using var auth = new EaseeClient (httpClient: http);
		using var provider = new RecoveringTokenProvider (auth, "test@example.invalid", "synthetic", (next, _) => { ordering.Add (next.RequiresReauthentication ? "invalidate" : "save-new"); return Task.CompletedTask; }, Token (DateTimeOffset.UtcNow.AddMinutes (-1)));
		Assert.That (await provider.GetAccessTokenAsync (), Is.EqualTo ("access-new"));
		Assert.That (ordering, Is.EqualTo (new[] { "invalidate", "request", "save-new" }));
		}
	[Test]
	public async Task ConcurrentRestAndSignalrTokenRequestsShareOneLogin ()
		{
		using var handler = new Handler ();
		using var http = new HttpClient (handler);
		using var auth = new EaseeClient (httpClient: http);
		using var provider = new RecoveringTokenProvider (auth, "test@example.invalid", "synthetic", (_, _) => Task.CompletedTask, null);
		var tokens = await Task.WhenAll (Enumerable.Range (0, 10).Select (_ => provider.GetAccessTokenAsync ()));
		Assert.That (tokens, Is.All.EqualTo ("access-new"));
		Assert.That (handler.Calls.Count, Is.EqualTo (1));
		}
	[Test]
	public async Task PersistenceFailureBlocksRefreshBeforeNetworkUse ()
		{
		using var handler = new Handler ();
		using var http = new HttpClient (handler);
		using var auth = new EaseeClient (httpClient: http);
		using var provider = new RecoveringTokenProvider (auth, "test@example.invalid", "synthetic", (_, _) => Task.FromException (new IOException ("Synthetic write failure")), Token (DateTimeOffset.UtcNow.AddMinutes (-1)));
		await Assert.ThrowsAsync<IOException> (async () => await provider.GetAccessTokenAsync ());
		Assert.That (handler.Calls, Is.Empty);
		}
	private static EaseeToken Token (DateTimeOffset expiry) => new () { AccessToken = "access-old", RefreshToken = "refresh-old", ExpiresAt = expiry };
	private sealed class Handler : HttpMessageHandler
		{
		internal readonly List<string> Calls = [];
		internal Action? OnRequest;
		protected override Task<HttpResponseMessage> SendAsync (HttpRequestMessage request, CancellationToken cancellationToken)
			{
			Calls.Add (request.RequestUri!.AbsolutePath);
			OnRequest?.Invoke ();
			return Task.FromResult (new HttpResponseMessage (HttpStatusCode.OK)
				{
				Content = new StringContent ("{\"accessToken\":\"access-new\",\"refreshToken\":\"refresh-new\",\"expiresIn\":3600}", Encoding.UTF8, "application/json")
				});
			}
		}
	}