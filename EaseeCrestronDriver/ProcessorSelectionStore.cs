using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Crestron.SimplSharp.CrestronDataStore;

using StoreError = Crestron.SimplSharp.CrestronDataStore.CrestronDataStore.CDS_ERROR;

namespace Easee.CrestronDriver;

// Hidden configuration items are not part of the Account wizard's saved step.
// Retain the user's explicit selection independently, scoped to this instance and account.
internal sealed class ProcessorSelectionStore
	{
	private readonly string _key;
	private readonly string _account;
	internal ProcessorSelectionStore (string driverId, string account)
		{
		_account = account;
		using var hash = SHA256.Create ();
		_key = "EaseeS" + BitConverter.ToString (hash.ComputeHash (Encoding.UTF8.GetBytes (driverId))).Replace ("-", "").Substring (0, 24);
		Ensure (CrestronDataStoreStatic.InitCrestronDataStore ());
		}
	internal (string Site, string Charger)? Load ()
		{
		var result = CrestronDataStoreStatic.GetLocalByteValue (_key, out byte[] value);
		if (result == StoreError.CDS_RECORD_NOT_FOUND)
			return null;
		Ensure (result);
		if (value is null || value.Length == 0)
			return null;
		var saved = JsonSerializer.Deserialize<Selection> (Encoding.UTF8.GetString (value));
		return saved?.Account == _account ? (saved.Site, saved.Charger) : null;
		}
	internal void Save (string site, string charger)
		{
		var value = Encoding.UTF8.GetBytes (JsonSerializer.Serialize (new Selection { Account = _account, Site = site, Charger = charger }));
		Ensure (CrestronDataStoreStatic.SetLocalByteValue (_key, value));
		Ensure (CrestronDataStoreStatic.GetLocalByteValue (_key, out byte[] saved));
		if (!saved.SequenceEqual (value))
			throw new IOException ("The selected site and charger could not be verified in processor storage.");
		}
	internal void Clear () => Ensure (CrestronDataStoreStatic.SetLocalByteValue (_key, []));
	private static void Ensure (StoreError error)
		{
		if (error != StoreError.CDS_SUCCESS)
			throw new IOException ("Processor selection storage failed: " + error);
		}
	private sealed class Selection
		{
		public string Account { get; set; } = "";
		public string Site { get; set; } = "";
		public string Charger { get; set; } = "";
		}
	}
