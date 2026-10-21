using System.Net;
using System.Text.Json;
using System.Xml.Linq;

using CrestronHomeDevTools;

using CrestronHomeNUnit.Android;

using EaseeChargerClient;
using EaseeChargerClient.Models;

namespace Easee.CrestronDriver.AndroidTests;

[TestFixture, NonParallelizable]
public sealed class PlacementUiTests
	{
	private const string Package = "com.crestron.phoenix.app";
	private const string Prefix = "com.crestron.phoenix.app:id/";
	private static readonly JsonSerializerOptions Json = new () { PropertyNameCaseInsensitive = true };
	private ConfigurationClient _processor = null!;
	private AndroidWorkflowSession _session = null!;
	private CrestronHomeNavigation _navigation = null!;
	private AndroidRunContext _context = null!;
	private Settings _settings = null!;
	private string _title = "", _tile = "", _page = "Home", _origin = "Home";
	private Action<AndroidHierarchy>? _pendingNavigation;
	private bool _uncertainControl, _needsStop;
	private int _capture;
	private sealed record Settings (string Host, string UserName, string Password, string CertificateSha256,
		 string EaseeUserName, string EaseePassword, string SiteId, string ChargerId, string AccessToken, DateTimeOffset ExpiresAt);
	private sealed record Activity (string Epoch, long Completed, int Pending);
	private sealed class Access (Settings settings) : IEaseeTokenProvider
		{
		public Task<string> GetAccessTokenAsync (CancellationToken cancellationToken = default)
			{
			cancellationToken.ThrowIfCancellationRequested ();
			if (settings.ExpiresAt <= DateTimeOffset.UtcNow)
				throw new InvalidOperationException ("The independent observer token expired.");
			return Task.FromResult (settings.AccessToken);
			}
		}

	[Test] public Task RoomPageTileIsVisibleNavigableAndOperable () => Run ("Room page", false, true);
	[Test] public Task HomePageTileIsVisibleNavigableAndOperable () => Run ("Home page", true, false);
	[Test] public Task BothTilesAreVisibleNavigableAndOperable () => Run ("Both", true, true);

	private async Task Run (string placement, bool home, bool room)
		{
		string? contextPath = Environment.GetEnvironmentVariable (AndroidWorkflowSession.CONTEXT_VARIABLE);
		if (string.IsNullOrWhiteSpace (contextPath))
			Assert.Ignore ("Run this fixture through the public installed-driver workflow.");
		if (Environment.GetEnvironmentVariable ("EASEE_DRIVER_UI_ALLOW_START_STOP") != "1")
			throw new InvalidOperationException ("The selected UI control run requires its separate start/stop opt-in.");
		_context = AndroidWorkflowSession.Read<AndroidRunContext> (contextPath!);
		if (Guid.Parse (_context.DriverGuid) != Guid.Parse ("e946ddc3-73f0-4dcd-ace7-e9528cfc8aae"))
			throw new InvalidDataException ("Wrong driver binding.");
		_settings = JsonSerializer.Deserialize<Settings> (File.ReadAllText (Environment.GetEnvironmentVariable ("EASEE_DRIVER_UI_SETTINGS") ?? throw new InvalidDataException ("Private UI settings missing.")), Json)!;
		if (_settings.Host != _context.ProcessorAddress || string.IsNullOrWhiteSpace (_settings.ChargerId) || _settings.ExpiresAt <= DateTimeOffset.UtcNow.AddMinutes (8))
			throw new InvalidDataException ("Private selection/observer inputs are unavailable or too close to expiry.");
		using var active = new CancellationTokenSource (TimeSpan.FromMinutes (24));
		var token = active.Token;
		await using var processor = await ConfigurationClient.ConnectAsync (new ()
			{
			Host = _settings.Host,
			CertificateSha256 = _settings.CertificateSha256
			}, new NetworkCredential (_settings.UserName, _settings.Password), token);
		_processor = processor;
		await PrepareSelectedCharger (token);
		var before = await Read (token);
		_tile = before.Name!;
		_title = "Easee " + before.PropertyValues["chargerName"].GetString ();
		string baseline = Placement (before);
		_session = await AndroidWorkflowSession.OpenFromEnvironmentAsync (token);
		_navigation = new (_session);
		bool restored = false;
		using var observer = new EaseeClient (new EaseeClientOptions { TokenProvider = new Access (_settings) });
		try
			{
			await _navigation.VerifySavedEndpointAsync ("easee.placement-endpoint", _context.Profile.LocalPort, token);
			await SetPlacement (placement, token);
			await AssertVisibility ("Home", home, token);
			await OpenRoomWhenConnected (token);
			_page = "Room";
			await AssertVisibility ("Room", room, token);
			await _navigation.RestoreHomeAndCaptureAsync ("easee.visibility-home", token);
			_page = "Home";
			foreach (string location in home && room ? new[] { "Home", "Room" } : new[] { home ? "Home" : "Room" })
				{
				if (location == "Room")
					{
					await OpenRoomWhenConnected (token);
					_page = "Room";
					}
				await AssertVisibility (location, true, token);
				await ExerciseTile (location, observer, token);
				}
			Assert.That ((await Read (token)).PropertyValues["selectedCharger"].GetString (), Is.EqualTo (_settings.ChargerId));
			}
		finally
			{
			using var cleanup = new CancellationTokenSource (TimeSpan.FromMinutes (5));
			try
				{
				if (_uncertainControl)
					throw new InvalidOperationException ("An unconfirmed UI charging command requires reconciliation; no gesture was replayed.");
				if (_needsStop)
					throw new InvalidOperationException ("The start/stop sequence has not confirmed its stop outcome.");
				await RestoreHome (cleanup.Token);
				await SetPlacement (baseline, cleanup.Token);
				var after = await Read (cleanup.Token);
				Assert.That (after.PropertyValues["selectedSite"].GetString () == _settings.SiteId && after.PropertyValues["selectedCharger"].GetString () == _settings.ChargerId, Is.True, "Selection changed during placement tests.");
				await RequireNoncharging (observer, cleanup.Token);
				await _session.CaptureAsync ("easee.placement-final-home", Home, cleanup.Token);
				restored = true;
				}
			finally { _session.Complete (restorationConfirmed: restored); }
			}
		}

	private async Task<DeviceInfo> Read (CancellationToken token)
		{
		var d = await _processor.GetDeviceAsync (_context.InstalledDriverId, token) ?? throw new InvalidDataException ("Candidate disappeared.");
		if (d.Model != "Easee Charger" || d.PropertyValues["cp.driverInformation:version"].GetString () != _context.DriverVersion || !d.PropertyValues["cp.driverConfiguration:isConfigured"].GetBoolean ())
			throw new InvalidDataException ("Candidate identity/configuration differs.");
		return d;
		}
	private async Task<DeviceInfo> WaitNative (Func<DeviceInfo, bool> predicate, CancellationToken token)
		{
		using var limit = CancellationTokenSource.CreateLinkedTokenSource (token);
		limit.CancelAfter (TimeSpan.FromSeconds (90));
		while (true)
			{
			var d = await Read (limit.Token);
			if (predicate (d))
				return d;
			await Task.Delay (250, limit.Token);
			}
		}
	private async Task PrepareSelectedCharger (CancellationToken token)
		{
		var d = await Read (token);
		foreach (var entry in new[] { (Property: "selectedSite", Options: "siteOptions", Command: "setSelectedSite", Value: _settings.SiteId), (Property: "selectedCharger", Options: "chargerOptions", Command: "setSelectedCharger", Value: _settings.ChargerId) })
			{
			d = await WaitNative (value => value.PropertyValues[entry.Options].EnumerateArray ().Any (o => o.GetProperty ("value").GetString () == entry.Value), token);
			string selected = d.PropertyValues[entry.Property].GetString ()!;
			if (selected == entry.Value)
				continue;
			if (selected != "" || !d.Commands.Contains (entry.Command))
				throw new InvalidDataException ("A different explicit selection is already configured.");
			string journal = Path.Combine (_context.EvidenceDirectory, "prepare-" + entry.Property + ".json");
			Intent (journal, "Select explicitly supplied device");
			await _processor.ExecuteDeviceCommandAsync (d.Id, entry.Command, new
				{
				value = entry.Value
				}, token);
			await WaitNative (value => value.PropertyValues[entry.Property].GetString () == entry.Value, token);
			}
		await WaitNative (value => value.PropertyValues["online"].GetBoolean () && value.PropertyValues["connectionKnown"].GetBoolean (), token);
		}
	private static string Placement (DeviceInfo d)
		{
		var tile = XDocument.Parse (d.PropertyValues["extension:uiDefinition"].GetString ()!).Root!.Element ("tile")!;
		bool home = (string?)tile.Attribute ("showinhomepage") == "#true", room = (string?)tile.Attribute ("showinroompage") == "#true";
		return home && room ? "Both" : home ? "Home page" : room ? "Room page" : throw new InvalidDataException ("Neither placement is enabled.");
		}
	private async Task SetPlacement (string placement, CancellationToken token)
		{
		if (Placement (await Read (token)) == placement)
			return;
		var step = await _processor.ExecuteDeviceCommandAsync (_context.InstalledDriverId, "cp.driverConfiguration:getFirstConfigurationStep", new
			{
			isReconfiguring = true
			}, token);
		if (step is not { ValueKind: JsonValueKind.Object } || step.Value.GetProperty ("Id").GetString () != "Account")
			throw new InvalidDataException ("Unexpected configuration wizard.");
		var values = new Dictionary<string, string> { { "Username", _settings.EaseeUserName }, { "Password", _settings.EaseePassword }, { "TilePlacement", placement } };
		foreach (string key in values.Keys)
			{
			var matches = step.Value.GetProperty ("Items").EnumerateArray ().Where (i => i.GetProperty ("Id").GetString () == key).ToArray ();
			if (matches.Length != 1 || matches[0].GetProperty ("Value").GetProperty ("ReadOnly").ValueKind != JsonValueKind.False)
				throw new InvalidDataException ("Configuration item is not uniquely writable.");
			}
		var result = await _processor.ExecuteDeviceCommandAsync (_context.InstalledDriverId, "cp.driverConfiguration:applyConfigurationStep", new
			{
			stepId = "Account",
			configurationItemValues = values,
			isReconfiguring = true
			}, token);
		values.Clear ();
		if (result is { ValueKind: not JsonValueKind.Null })
			throw new InvalidDataException ("Placement configuration did not complete; it was not repeated.");
		await WaitNative (d => Placement (d) == placement && d.PropertyValues["selectedSite"].GetString () == _settings.SiteId && d.PropertyValues["selectedCharger"].GetString () == _settings.ChargerId && d.PropertyValues["online"].GetBoolean () && d.PropertyValues["connectionKnown"].GetBoolean (), token);
		}

	private void Home (AndroidHierarchy h) => CrestronHomePages.RequireHome (h, _context.Profile.ExpectedHomeText);
	private async Task OpenRoomWhenConnected (CancellationToken token)
		{
		for (int attempt = 0; ; attempt++)
			{
			try
				{
				await _navigation.OpenRoomAsync ("Office", token);
				return;
				}
			catch (InvalidOperationException) when (attempt < 2)
				{
				// Retry only navigation, after observing the specific connecting screen and
				// independently restoring the known Home. Charging gestures never use this path.
				var gap = await _session.Device.CaptureAsync (token);
				if (!CrestronHomePages.IsConnecting (gap))
					throw;
				string id = "navigation-connection-gap-" + (_capture++);
				File.WriteAllText (Path.Combine (_context.EvidenceDirectory, id + ".xml"), gap.MaskedXml);
				await WaitPage (hierarchy =>
					{
						if (CrestronHomePages.IsConnecting (hierarchy))
							throw new InvalidOperationException ("The app is still connecting.");
					}, token);
				await _navigation.RestoreHomeAndCaptureAsync (id + "-restored", token);
				_page = "Home";
				await _session.CaptureAsync (id + "-before-retry", Home, token);
				}
			}
		}
	private void Room (AndroidHierarchy h) => CrestronHomePages.RequireRoom (h, "Office");
	private AndroidHierarchy Main (AndroidHierarchy h) => CrestronHomeExtensionPages.RequirePage (h, [_title]);
	private void Guard (AndroidHierarchy h, string page)
		{
		if (page == "Home")
			Home (h);
		else if (page == "Room")
			Room (h);
		else
			Main (h);
		}
	private async Task WaitPage (Action<AndroidHierarchy> guard, CancellationToken token)
		{
		using var limit = CancellationTokenSource.CreateLinkedTokenSource (token);
		limit.CancelAfter (TimeSpan.FromSeconds (90));
		while (true)
			{
			try
				{
				guard (await _session.Device.CaptureAsync (limit.Token));
				return;
				}
			catch (Exception e) when (e is InvalidOperationException or InvalidDataException) { await Task.Delay (250, limit.Token); }
			}
		}
	private async Task Tap (Func<AndroidHierarchy, AndroidElement> select, Action<AndroidHierarchy> source, Action<AndroidHierarchy> destination, string page, CancellationToken token)
		{
		if (_pendingNavigation != null)
			throw new InvalidOperationException ("Earlier UI input remains unconfirmed.");
		await TapElement (select, source, () => { _pendingNavigation = destination; _page = page; }, token);
		await WaitPage (destination, token);
		_pendingNavigation = null;
		}
	private static AndroidSelector Id (string id) => CrestronHomePages.Resource (id);
	private static AndroidSelector Text (string text) => new (AndroidSelectorKind.Text, text);
	private AndroidSelector TileSelector (AndroidHierarchy hierarchy, string page)
		{
		if (page == "Home")
			{
			var root = XDocument.Parse (hierarchy.MaskedXml).Descendants ("node").Single (n => (string?)n.Attribute ("resource-id") == Prefix + "fragmentHomeContainer");
			var scope = new AndroidHierarchy (new XElement ("hierarchy", new XElement (root)).ToString (), Package);
			return UiText.Selector (scope, _tile) with { AncestorResourceId = Prefix + "fragmentHomeContainer" };
			}
		var values = XDocument.Parse (hierarchy.MaskedXml).Descendants ("node").Where (n => (string?)n.Attribute ("package") == Package && string.Equals ((string?)n.Attribute ("content-desc"), "room_service_" + _tile, StringComparison.OrdinalIgnoreCase)).Select (n => (string)n.Attribute ("content-desc")!).ToArray ();
		if (values.Length != 1)
			throw new InvalidDataException ("Room tile is missing or ambiguous ignoring case.");
		return new (AndroidSelectorKind.ContentDescription, values[0]);
		}
	private XElement? TileNode (AndroidHierarchy h, string page)
		{
		var nodes = XDocument.Parse (h.MaskedXml).Descendants ("node");
		var found = nodes.Where (n => (string?)n.Attribute ("package") == Package && (page == "Room" ? string.Equals ((string?)n.Attribute ("content-desc"), "room_service_" + _tile, StringComparison.OrdinalIgnoreCase) : string.Equals ((string?)n.Attribute ("text"), _tile, StringComparison.OrdinalIgnoreCase) && n.Ancestors ("node").Any (a => (string?)a.Attribute ("resource-id") == Prefix + "fragmentHomeContainer"))).ToArray ();
		if (found.Length > 1)
			throw new InvalidDataException ("Tile is ambiguous.");
		if (found.Length == 0)
			return null;
		return page == "Room" ? found[0] : found[0].Ancestors ("node").FirstOrDefault (n => n.Descendants ("node").Any (c => (string?)c.Attribute ("resource-id") == Prefix + "titleSubtitle_subtitle")) ?? throw new InvalidDataException ("Unknown Home tile structure.");
		}
	private AndroidElement? Viewport (AndroidHierarchy h, string page)
		{
		if (page == "Room")
			{
			var container = h.RequireUnique (Id ("room_scrollView"));
			var back = h.RequireUnique (Id ("room_back"));
			var roomBar = h.RequireUnique (Id ("bottomNavigationView"));
			return container with
				{
				Top = Math.Max (container.Top, back.Bottom),
				Bottom = Math.Min (container.Bottom, roomBar.Top)
				};
			}
		var root = XDocument.Parse (h.MaskedXml).Descendants ("node").Single (n => (string?)n.Attribute ("resource-id") == Prefix + "fragmentHomeContainer");
		var scroll = root.DescendantsAndSelf ().Where (n => (string?)n.Attribute ("scrollable") == "true").ToArray ();
		if (scroll.Length > 1)
			throw new InvalidDataException ("Home scrolling area is ambiguous.");
		// The captured app has an anonymous ScrollView immediately above its Home container.
		// Select only that observed structural ancestor, never a screen-sized guessed gesture area.
		var node = scroll.SingleOrDefault () ?? root.Ancestors ("node").FirstOrDefault (n => (string?)n.Attribute ("package") == Package && (string?)n.Attribute ("scrollable") == "true") ?? root;
		var match = System.Text.RegularExpressions.Regex.Match ((string?)node.Attribute ("bounds") ?? "", @"^\[(\d+),(\d+)\]\[(\d+),(\d+)\]$");
		if (!match.Success)
			throw new InvalidDataException ("Observed Home viewport bounds are invalid.");
		int N (int index) => int.Parse (match.Groups[index].Value, System.Globalization.CultureInfo.InvariantCulture);
		var homeBar = h.RequireUnique (Id ("bottomNavigationView"));
		return new AndroidElement ((string?)node.Attribute ("resource-id") ?? "", "", "", (string?)node.Attribute ("enabled") == "true", N (1), N (2), N (3), Math.Min (N (4), homeBar.Top));
		}
	private static bool Inside (AndroidElement e, AndroidElement? viewport) => e.Enabled && (viewport is null || e.Left >= viewport.Left && e.Right <= viewport.Right && e.Top > viewport.Top && e.Bottom < viewport.Bottom);
	private async Task AssertVisibility (string page, bool expected, CancellationToken token)
		{
		bool found = false;
		foreach (bool down in new[] { true, false })
			{
			var seen = new HashSet<string> (StringComparer.Ordinal);
			bool end = false;
			for (int i = 0; i < 18; i++)
				{
				AndroidHierarchy? current = null;
				await _session.CaptureAsync ("placement-" + page + "-" + (_capture++), h => { Guard (h, page); current = h; }, token);
				var h = current!;
				var viewport = Viewport (h, page);
				var card = TileNode (h, page);
				if (card != null && Inside (h.RequireUnique (TileSelector (h, page)), viewport))
					{
					found = true;
					string expectedStatus = (await Read (token)).PropertyValues["tileDisplay"].GetString ()!;
					static string Normal (string value) => string.Concat (value.Where (c => !char.IsWhiteSpace (c)));
					Assert.That (card.DescendantsAndSelf ().Any (n => string.Equals (Normal ((string?)n.Attribute ("text") ?? ""), Normal (expectedStatus), StringComparison.OrdinalIgnoreCase)), Is.True, "Rendered tile status differs from current driver state.");
					if (expected)
						return;
					}
				string signature = string.Join ("|", XDocument.Parse (h.MaskedXml).Descendants ("node").Where (n => (string?)n.Attribute ("resource-id") == Prefix + "titleSubtitle_title" || ((string?)n.Attribute ("content-desc"))?.StartsWith ("room_service_", StringComparison.Ordinal) == true).Select (n => (string?)n.Attribute ("text") + ":" + (string?)n.Attribute ("content-desc") + ":" + (string?)n.Attribute ("bounds")));
				if (viewport == null || !seen.Add (signature))
					{
					end = true;
					break;
					}
				Func<AndroidHierarchy, AndroidElement> select = state => Viewport (state, page) ?? throw new InvalidDataException ("Scrolling area changed.");
				if (down)
					await ScrollObserved (true, select, hierarchy => Guard (hierarchy, page), () => { }, token);
				else
					await ScrollObserved (false, select, hierarchy => Guard (hierarchy, page), () => { }, token);
				await Task.Delay (500, token);
				}
			if (!end)
				throw new InvalidDataException ("The complete tile page was not observed within the bounded scroll search.");
			}
		Assert.That (found, Is.EqualTo (expected), "Actual tile visibility differs from configured placement.");
		}
	private AndroidElement NavigationButton (AndroidHierarchy h, string location)
		{
		var card = TileNode (h, location) ?? throw new InvalidDataException ("Tile is missing.");
		var scope = new AndroidHierarchy (new XElement ("hierarchy", new XElement (card)).ToString (), Package);
		var arrow = location == "Room" ? h.RequireUnique (Id ("serviceDots") with
			{
			SiblingText = UiText.Selector (h, _tile).Value
			}) : scope.RequireUnique (Id ("serviceDots"));
		if (!Inside (arrow, Viewport (h, location)))
			throw new InvalidDataException ("Navigation control is not fully visible.");
		return arrow;
		}
	private async Task ExerciseTile (string location, EaseeClient observer, CancellationToken token)
		{
		if (location == "Room")
			await _session.CaptureAsync ("placement-Room-action-navigation-surfaces", hierarchy =>
				{
					Guard (hierarchy, location);
					_ = NavigationButton (hierarchy, location);
				}, token);
		await RequireNoncharging (observer, token);
		var native = await WaitNative (d => d.PropertyValues["canCharge"].GetBoolean (), token);
		Activity baseline = ActivityOf (native);
		if (baseline.Pending != 0)
			throw new InvalidDataException ("A charger command is already active.");
		_origin = location;
		if (location == "Home")
			{
			// This app's Home tile navigates; its detail page provides the actual controls.
			// Prove that navigation alone did not invoke the tile's declared charging command.
			Intent (Path.Combine (_context.EvidenceDirectory, "navigate-Home.json"), "Home navigation; expect no charging command");
			_needsStop = true;
			_uncertainControl = true;
			await Tap (h => h.RequireUnique (TileSelector (h, "Home")), h => Home (h), h => Main (h), "Main", token);
			for (int sample = 0; sample < 2; sample++)
				{
				Assert.That (ActivityOf (await Read (token)), Is.EqualTo (baseline), "Home navigation unexpectedly issued a charger command.");
				if (sample == 0)
					await Task.Delay (1000, token);
				}
			_uncertainControl = false;
			_needsStop = false;
			File.WriteAllText (Path.Combine (_context.EvidenceDirectory, "HomeNavigationObserved.json"), JsonSerializer.Serialize (new
				{
				HomeTapOpenedDetails = true,
				ChargerCommandIssuedByNavigation = false
				}));
			await _session.CaptureAsync ("placement-Home-details-before-control", h => { var main = Main (h); RequireText (main, "Status"); RequireText (main, "Charging power"); }, token);
			await RevealControl ("Charge now", token);
			baseline = ActivityOf (await Read (token));
			}
		Intent (Path.Combine (_context.EvidenceDirectory, "start-" + location + ".json"), "UI immediate-start intent");
		_needsStop = true;
		_uncertainControl = true;
		if (location == "Home")
			await TapElement (h => VisibleMainControl (h, "Charge now"), h => Main (h), () => { }, token);
		else
			await TapElement (h => h.RequireUnique (TileSelector (h, location)), h => { Guard (h, location); if (!Inside (h.RequireUnique (TileSelector (h, location)), Viewport (h, location))) throw new InvalidDataException ("Action surface is not visible."); }, () => { }, token);
		await CompleteCommand (baseline, "Immediate charging requested", token);
		_uncertainControl = false;
		if (location == "Room")
			await Tap (h => NavigationButton (h, location), h => Guard (h, location), h => Main (h), "Main", token);
		await _session.CaptureAsync ("placement-" + location + "-details", h => { var main = Main (h); RequireText (main, "Status"); RequireText (main, "Charging power"); }, token);
		await RevealControl ("Stop charging", token);
		baseline = ActivityOf (await Read (token));
		Intent (Path.Combine (_context.EvidenceDirectory, "stop-" + location + ".json"), "UI stop intent");
		_uncertainControl = true;
		await TapElement (h => VisibleMainControl (h, "Stop charging"), h => Main (h), () => { }, token);
		await CompleteCommand (baseline, "Charging stop requested", token);
		_uncertainControl = false;
		await RequireNoncharging (observer, token);
		_needsStop = false;
		await RestoreHome (token);
		}
	private static Activity ActivityOf (DeviceInfo d) => JsonSerializer.Deserialize<Activity> (d.PropertyValues["commandActivity"].GetString ()!, Json)!;
	private async Task CompleteCommand (Activity before, string expected, CancellationToken token)
		{
		var after = await WaitNative (d => { var current = ActivityOf (d); if (current.Epoch != before.Epoch || current.Completed > before.Completed + 1) throw new InvalidDataException ("Command attribution changed."); return current.Completed == before.Completed + 1 && current.Pending == 0; }, token);
		Assert.That (after.PropertyValues["commandStatus"].GetString (), Is.EqualTo (expected), "The UI action did not receive the expected charger request response.");
		}
	private async Task RequireNoncharging (EaseeClient observer, CancellationToken token)
		{
		for (int sample = 0; sample < 2; sample++)
			{
			var response = await observer.GetobservationsAsync (_settings.ChargerId, "109,250", cancellationToken: token);
			var values = response.Value?.Observations ?? throw new InvalidDataException ("Independent charger state is missing.");
			var mode = values.OfType<EaseeIntegerObservation> ().Single (o => o.Id == 109).Value;
			var online = values.OfType<EaseeBooleanObservation> ().Single (o => o.Id == 250).Value;
			Assert.That (online == true && mode is >= 1 and <= 8 && mode is not (3 or 5), Is.True, "Independent read did not confirm an online noncharging car.");
			if (sample == 0)
				await Task.Delay (1000, token);
			}
		}
	private static AndroidElement TextElement (AndroidHierarchy h, string text) => h.RequireUnique (UiText.Selector (h, text));
	private static void RequireText (AndroidHierarchy h, string text) => _ = TextElement (h, text);
	private AndroidElement VisibleMainControl (AndroidHierarchy hierarchy, string label)
		{
		var main = Main (hierarchy);
		var control = TextElement (main, label);
		if (!Inside (control, main.RequireUnique (Id ("customdevices_componentRecyclerView"))))
			throw new InvalidDataException ("The requested charging control is not fully visible.");
		return control;
		}
	private async Task RevealControl (string label, CancellationToken token)
		{
		for (int i = 0; i < 12; i++)
			{
			var root = Main (await _session.Device.CaptureAsync (token));
			try
				{
				var control = TextElement (root, label);
				if (Inside (control, root.RequireUnique (Id ("customdevices_componentRecyclerView"))))
					return;
				}
			catch (InvalidDataException) { }
			await ScrollObserved (true, h => Main (h).RequireUnique (Id ("customdevices_componentRecyclerView")), h => Main (h), () => { }, token);
			await Task.Delay (500, token);
			}
		throw new InvalidDataException ("Required charging control was not fully visible: " + label);
		}
	private async Task RestoreHome (CancellationToken token)
		{
		if (_pendingNavigation != null)
			{
			await WaitPage (_pendingNavigation, token);
			_pendingNavigation = null;
			}
		if (_page == "Main")
			await Tap (h => Main (h).RequireUnique (Id ("customdevices_toolbarClose")), h => Main (h), h => Guard (h, _origin), _origin, token);
		await _navigation.RestoreHomeAndCaptureAsync ("easee.restore-" + (_capture++), token);
		_page = "Home";
		}
	// As in the Tesla model, public ADB transport uses bounds from a fresh guarded hierarchy.
	// Internal AndroidDevice callbacks are not part of the published fixture API.
	private async Task TapElement (Func<AndroidHierarchy, AndroidElement> select, Action<AndroidHierarchy> guard, Action beforeSend, CancellationToken token)
		{
		AndroidWorkflowSession.VerifyContext (_session.Context);
		var hierarchy = await _session.Device.CaptureAsync (token);
		guard (hierarchy);
		var target = select (hierarchy);
		if (!target.Enabled || target.Right <= target.Left || target.Bottom <= target.Top)
			throw new InvalidDataException ("UI target is disabled or has invalid observed bounds.");
		RetainInput ("tap", hierarchy, target);
		AndroidWorkflowSession.VerifyContext (_session.Context);
		token.ThrowIfCancellationRequested ();
		beforeSend ();
		var profile = _session.Context.Profile;
		var transport = new AdbCommandTransport (profile.AdbExecutable, profile.DeviceSerial, TimeSpan.FromSeconds (25));
		await transport.ExecuteAsync (["shell", "input", "tap", Number ((target.Left + target.Right) / 2), Number ((target.Top + target.Bottom) / 2)], token);
		}
	private async Task ScrollObserved (bool down, Func<AndroidHierarchy, AndroidElement> select, Action<AndroidHierarchy> guard, Action beforeSend, CancellationToken token)
		{
		AndroidWorkflowSession.VerifyContext (_session.Context);
		var hierarchy = await _session.Device.CaptureAsync (token);
		guard (hierarchy);
		var viewport = select (hierarchy);
		if (!viewport.Enabled || viewport.Right - viewport.Left < 80 || viewport.Bottom - viewport.Top < 100)
			throw new InvalidDataException ("Observed scroll viewport is unusable.");
		int x = (viewport.Left + viewport.Right) / 2, high = viewport.Top + (viewport.Bottom - viewport.Top) / 4, low = viewport.Top + 3 * (viewport.Bottom - viewport.Top) / 4;
		RetainInput (down ? "scroll-down" : "scroll-up", hierarchy, viewport);
		AndroidWorkflowSession.VerifyContext (_session.Context);
		token.ThrowIfCancellationRequested ();
		beforeSend ();
		var profile = _session.Context.Profile;
		var transport = new AdbCommandTransport (profile.AdbExecutable, profile.DeviceSerial, TimeSpan.FromSeconds (25));
		await transport.ExecuteAsync (["shell", "input", "swipe", Number (x), Number (down ? low : high), Number (x), Number (down ? high : low), "350"], token);
		}
	private static string Number (int value) => value.ToString (System.Globalization.CultureInfo.InvariantCulture);
	private void RetainInput (string action, AndroidHierarchy hierarchy, AndroidElement element)
		{
		string folder = Path.Combine (_context.EvidenceDirectory, "placement-inputs");
		Directory.CreateDirectory (folder);
		string id = Guid.NewGuid ().ToString ("N");
		File.WriteAllText (Path.Combine (folder, id + ".xml"), hierarchy.MaskedXml);
		File.WriteAllText (Path.Combine (folder, id + ".json"), JsonSerializer.Serialize (new
			{
			Action = action,
			Element = element,
			Utc = DateTimeOffset.UtcNow
			}));
		}
	private static void Intent (string path, string phase)
		{
		using var file = new FileStream (path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
		byte[] bytes = System.Text.Encoding.UTF8.GetBytes (JsonSerializer.Serialize (new
			{
			Phase = phase,
			Utc = DateTimeOffset.UtcNow
			}));
		file.Write (bytes);
		file.Flush (true);
		}
	}