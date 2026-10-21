using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Crestron.SimplSharp.CrestronDataStore;

using EaseeChargerClient;

using StoreError = Crestron.SimplSharp.CrestronDataStore.CrestronDataStore.CDS_ERROR;

namespace Easee.CrestronDriver;

// One native byte record commits the pair, expiry and refresh-in-flight marker together.
// String records are limited to 1600 characters and cannot hold real Easee token pairs.
// Never place these values in UI properties or logs. UI masking is not an encryption guarantee.
internal sealed class ProcessorTokenStore
	{
	private readonly string _key;
	private readonly string _account;
	internal ProcessorTokenStore (string driverId, string account)
		{
		_account = account;
		using var hash = SHA256.Create ();
		_key = "EaseeB" + BitConverter.ToString (hash.ComputeHash (Encoding.UTF8.GetBytes (driverId))).Replace ("-", "").Substring (0, 24);
		Ensure (CrestronDataStoreStatic.InitCrestronDataStore ());
		}
	internal EaseeToken? Load ()
		{
		var result = CrestronDataStoreStatic.GetLocalByteValue (_key, out byte[] value);
		if (result == StoreError.CDS_RECORD_NOT_FOUND)
			return null;
		Ensure (result);
		if (value is null || value.Length == 0)
			return null;
		var record = JsonSerializer.Deserialize<SavedSession> (Encoding.UTF8.GetString (value));
		return string.Equals (record?.Account, _account, StringComparison.Ordinal) ? record!.Token : null;
		}
	internal Task SaveAsync (EaseeToken token, CancellationToken cancellationToken)
		{
		cancellationToken.ThrowIfCancellationRequested ();
		var value = Encoding.UTF8.GetBytes (JsonSerializer.Serialize (new SavedSession { Account = _account, Token = token }));
		Ensure (CrestronDataStoreStatic.SetLocalByteValue (_key, value));
		Ensure (CrestronDataStoreStatic.GetLocalByteValue (_key, out byte[] saved));
		if (!saved.SequenceEqual (value))
			throw new IOException ("The token checkpoint could not be verified.");
		return Task.CompletedTask;
		}
	internal void Clear () => Ensure (CrestronDataStoreStatic.SetLocalByteValue (_key, []));
	private static void Ensure (StoreError result)
		{
		if (result != StoreError.CDS_SUCCESS)
			throw new IOException ("Processor token storage failed: " + result);
		}
	private sealed class SavedSession
		{
		public string Account { get; set; } = "";
		public EaseeToken? Token
			{
			get; set;
			}
		}
	}