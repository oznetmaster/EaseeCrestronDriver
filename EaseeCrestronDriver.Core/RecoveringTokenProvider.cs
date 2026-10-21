using System.Net;

using EaseeChargerClient;

namespace Easee.CrestronDriver;

// Owns a single token chain. Fresh login occurs during token acquisition, never by replaying a REST write.
internal sealed class RecoveringTokenProvider : IEaseeTokenProvider, IDisposable
	{
	private readonly EaseeSession _session;
	private readonly string _username;
	private readonly string _password;
	private readonly SemaphoreSlim _gate = new (1, 1);
	private bool _loginRequired;
	internal RecoveringTokenProvider (EaseeClient authentication, string username, string password,
		 Func<EaseeToken, CancellationToken, Task> persist, EaseeToken? initialToken)
		{
		_username = username;
		_password = password;
		_session = new EaseeSession (authentication, persist, initialToken);
		_loginRequired = initialToken is null || initialToken.RequiresReauthentication;
		}
	public async Task<string> GetAccessTokenAsync (CancellationToken cancellationToken = default)
		{
		await _gate.WaitAsync (cancellationToken).ConfigureAwait (false);
		try
			{
			if (_loginRequired)
				{
				await _session.SignInAsync (_username, _password, cancellationToken).ConfigureAwait (false);
				_loginRequired = false;
				}
			try
				{
				return await _session.GetAccessTokenAsync (cancellationToken).ConfigureAwait (false);
				}
			catch (InvalidOperationException)
				{
				_loginRequired = true;
				await _session.SignInAsync (_username, _password, cancellationToken).ConfigureAwait (false);
				_loginRequired = false;
				return await _session.GetAccessTokenAsync (cancellationToken).ConfigureAwait (false);
				}
			catch (EaseeApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
				{
				_loginRequired = true;
				throw;
				}
			}
		finally { _gate.Release (); }
		}
	internal async Task RequireFreshLoginAsync (CancellationToken cancellationToken)
		{
		await _gate.WaitAsync (cancellationToken).ConfigureAwait (false);
		try
			{
			_loginRequired = true;
			}
		finally { _gate.Release (); }
		}
	public void Dispose ()
		{
		_session.Dispose ();
		_gate.Dispose ();
		}
	}