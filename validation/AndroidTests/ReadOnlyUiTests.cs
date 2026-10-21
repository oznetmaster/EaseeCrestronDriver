using System.Net;
using System.Text.Json;

using CrestronHomeDevTools;

using CrestronHomeNUnit.Android;

namespace Easee.CrestronDriver.AndroidTests;

[TestFixture, NonParallelizable]
public sealed class ReadOnlyUiTests
	{
	// Locate display text without assuming its presentation casing. Resource IDs and selected values remain exact.
	private static AndroidSelector Text (AndroidHierarchy page, string value) => UiText.Selector (page, value);
	private sealed record Settings (string Host, string UserName, string Password, string CertificateSha256);
	private sealed record Choice (string Value, Label Label);
	private sealed record Label (string Text);
	private sealed record Activity (string Epoch, long Completed, int Pending);

	[Test]
	public async Task RoomTileNavigatesAndAccountPickersAreAccessible ()
		{
		var contextPath = Environment.GetEnvironmentVariable (AndroidWorkflowSession.CONTEXT_VARIABLE);
		if (string.IsNullOrWhiteSpace (contextPath))
			Assert.Ignore ("Run through the public installed-driver workflow.");
		var context = AndroidWorkflowSession.Read<AndroidRunContext> (contextPath!);
		if (Guid.Parse (context.DriverGuid) != Guid.Parse ("e946ddc3-73f0-4dcd-ace7-e9528cfc8aae"))
			throw new InvalidDataException ("The workflow selected another driver.");
		var settingsPath = Environment.GetEnvironmentVariable ("EASEE_DRIVER_UI_SETTINGS") ?? throw new InvalidOperationException ("Private processor settings are required.");
		var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
		var settings = JsonSerializer.Deserialize<Settings> (File.ReadAllText (settingsPath), options)!;
		if (settings.Host != context.ProcessorAddress)
			throw new InvalidDataException ("The private settings select a different processor.");
		using var active = new CancellationTokenSource (TimeSpan.FromMinutes (24));
		async Task<ConfigurationClient> Connect () => await ConfigurationClient.ConnectAsync (new ()
			{
			Host = settings.Host,
			CertificateSha256 = settings.CertificateSha256
			}, new NetworkCredential (settings.UserName, settings.Password), active.Token);
		async Task<DeviceInfo> Read ()
			{
			await using var client = await Connect ();
			var value = await client.GetDeviceAsync (context.InstalledDriverId, active.Token) ?? throw new InvalidDataException ("Selected driver missing.");
			if (value.Model != "Easee Charger" || value.PropertyValues["cp.driverInformation:version"].GetString () != context.DriverVersion || !value.PropertyValues["cp.driverConfiguration:isConfigured"].GetBoolean ())
				throw new InvalidDataException ("Selected driver is not the online candidate.");
			return value;
			}
		var before = await Read ();
		await using (var discovery = await Connect ())
			{
			if ((await discovery.GetDevicesAsync (active.Token)).Count (d => string.Equals (d.Name, before.Name, StringComparison.OrdinalIgnoreCase)) != 1)
				throw new InvalidDataException ("Tile name is ambiguous ignoring case.");
			}
		string title = "Easee " + before.PropertyValues["chargerName"].GetString ();
		var sites = JsonSerializer.Deserialize<Choice[]> (before.PropertyValues["siteOptions"], options)!;
		var chargers = JsonSerializer.Deserialize<Choice[]> (before.PropertyValues["chargerOptions"], options)!;
		string site = before.PropertyValues["selectedSite"].GetString ()!, charger = before.PropertyValues["selectedCharger"].GetString ()!;
		var session = await AndroidWorkflowSession.OpenFromEnvironmentAsync (active.Token);
		var navigation = new CrestronHomeNavigation (session);
		bool restored = false;
		try
			{
			await navigation.VerifySavedEndpointAsync ("easee.endpoint", context.Profile.LocalPort, active.Token);
			await navigation.InspectRoomDetailsPagesAsync ("easee.room", "Office", before.Name!, title, async (pages, token) =>
				 {
					 await pages.InspectAsync ("easee.main", page =>
							  {
								  page.RequireUnique (Text (page, "Status"));
								  page.RequireUnique (Text (page, "Charging power"));
								  page.RequireUnique (Text (page, "Session energy"));
							  }, token);
					 var settingsControl = await Reveal (pages, "Settings", token);
					 await pages.OpenPageAsync (settingsControl, "Settings", CrestronHomePages.Resource ("customdevices_toolbarClose"), token);
					 try
						 {
					 var siteControl = await Reveal (pages, "Site", token);
					 if (!string.IsNullOrEmpty (site))
						 await InspectSelection (session, title, "easee.sites", siteControl, sites.Select (s => s.Label.Text).ToArray (), sites.Single (s => s.Value == site).Label.Text, token);
					 else
						 await pages.InspectAsync ("easee.site-picker-unselected", p => Assert.That (p.RequireUnique (siteControl).Enabled, Is.True), token);
					 var chargerControl = await Reveal (pages, "Charger", token);
					 if (!string.IsNullOrEmpty (charger))
						 await InspectSelection (session, title, "easee.chargers", chargerControl, chargers.Select (c => c.Label.Text).ToArray (), chargers.Single (c => c.Value == charger).Label.Text, token);
					 else
						 await pages.InspectAsync ("easee.charger-picker-unselected", p => p.RequireUnique (chargerControl), token);
						 }
					 finally
						 {
						 using var close = new CancellationTokenSource (TimeSpan.FromMinutes (3));
						 await pages.ClosePageAsync (close.Token);
						 }
				 }, active.Token);
			var after = await Read ();
			Assert.That (after.PropertyValues["selectedSite"].GetString () == site && after.PropertyValues["selectedCharger"].GetString () == charger, Is.True, "Picker inspection changed selection.");
			var initialActivity = JsonSerializer.Deserialize<Activity> (before.PropertyValues["commandActivity"].GetString ()!, options)!;
			var finalActivity = JsonSerializer.Deserialize<Activity> (after.PropertyValues["commandActivity"].GetString ()!, options)!;
			Assert.That (finalActivity, Is.EqualTo (initialActivity), "Read-only picker inspection changed charger command activity.");
			Assert.That (finalActivity.Pending, Is.Zero);
			Assert.That (after.PropertyValues["online"].GetBoolean (), Is.True);
			Assert.That (after.PropertyValues["charging"].GetBoolean (), Is.False);
			File.WriteAllText (Path.Combine (context.EvidenceDirectory, "PickerInspectionObserved.json"), JsonSerializer.Serialize (new
				{
				SiteCount = sites.Length,
				ChargerCount = chargers.Length,
				SelectionPreserved = true,
				CommandActivityUnchanged = true,
				Online = true,
				Charging = false,
				Utc = DateTimeOffset.UtcNow
				}));
			}
		finally
			{
			using var cleanup = new CancellationTokenSource (TimeSpan.FromMinutes (3));
			try
				{
				await navigation.RestoreHomeAndCaptureAsync ("easee.final", cleanup.Token);
				restored = navigation.HomeRestored;
				}
			finally { session.Complete (restorationConfirmed: restored); }
			}
		}
	private static async Task InspectSelection (AndroidWorkflowSession session, string mainTitle, string checkId, AndroidSelector open, string[] expectedLabels, string expectedSelected, CancellationToken token)
		{
		if (expectedLabels.Length == 0 || expectedLabels.Distinct (StringComparer.OrdinalIgnoreCase).Count () != expectedLabels.Length || !expectedLabels.Contains (expectedSelected, StringComparer.OrdinalIgnoreCase))
			throw new InvalidDataException ("Expected picker labels are missing or ambiguous ignoring case.");
		AndroidHierarchy Page (AndroidHierarchy h) => CrestronHomeExtensionPages.RequirePage (h, [mainTitle, "Settings"]);
		AndroidHierarchy Selection (AndroidHierarchy h) => CrestronHomeExtensionPages.RequireSelection (h, [mainTitle, "Settings"]);
		void Intent (string action)
			{
			AndroidWorkflowSession.VerifyContext (session.Context);
			using var file = new FileStream (Path.Combine (session.Context.EvidenceDirectory, checkId + "-" + action + ".json"), FileMode.CreateNew);
			JsonSerializer.Serialize (file, new { NavigationOnly = true, Action = action, Utc = DateTimeOffset.UtcNow });
			file.Flush (true);
			}
		await session.CaptureAsync (checkId + ".before", h => Page (h).RequireUnique (open), token);
		Intent ("open");
		await session.Device.TapAsync (open, h => Page (h).RequireUnique (open), token);
		Exception? failure = null;
		try
			{
			var observed = new Dictionary<string, bool> (StringComparer.OrdinalIgnoreCase);
			IReadOnlyList<AndroidSelectionOption>? previous = null;
			bool complete = false;
			for (int page = 0; page < 64; page++)
				{
				IReadOnlyList<AndroidSelectionOption> current = [];
				bool scrollable = false;
				await session.CaptureAsync (checkId + ".options-" + page, h =>
					{
					var list = Selection (h);
					current = CrestronHomeExtensionPages.ReadSelectionOptions (list);
					var viewport = System.Xml.Linq.XDocument.Parse (list.MaskedXml).Descendants ("node").Single (node =>
						(string?)node.Attribute ("resource-id") == "com.crestron.phoenix.app:id/customdevice_selectionRecyclerView");
					if (!bool.TryParse ((string?)viewport.Attribute ("scrollable"), out scrollable))
						throw new InvalidDataException ("Picker scrolling capability was not observed.");
					foreach (var option in current)
						{
						if (!expectedLabels.Contains (option.Label, StringComparer.OrdinalIgnoreCase) || option.Selected != string.Equals (option.Label, expectedSelected, StringComparison.OrdinalIgnoreCase) || observed.TryGetValue (option.Label, out bool old) && old != option.Selected)
							throw new InvalidDataException ("Picker options or selected state differ from the configured device.");
						observed[option.Label] = option.Selected;
						}
					}, token);
				if (!scrollable)
					{
					// A non-scrollable list must expose its complete expected contents in this viewport.
					// Do not swipe selectable rows to infer an end which is already directly observed.
					if (current.Count != expectedLabels.Length)
						throw new InvalidDataException ("Non-scrollable picker did not expose every expected option.");
					complete = true;
					break;
					}
				if (previous != null && previous.Count == current.Count && previous.Zip (current).All (pair => string.Equals (pair.First.Label, pair.Second.Label, StringComparison.OrdinalIgnoreCase) && pair.First.Selected == pair.Second.Selected))
					{
					complete = true;
					break;
					}
				previous = current;
				var hierarchy = await session.Device.CaptureAsync (token);
				var viewport = Selection (hierarchy).RequireUnique (CrestronHomePages.Resource ("customdevice_selectionRecyclerView"));
				if (!viewport.Enabled || viewport.Bottom - viewport.Top < 100 || viewport.Right - viewport.Left < 80)
					throw new InvalidDataException ("Picker viewport has invalid observed bounds.");
				Intent ("scroll-" + page);
				int x = (viewport.Left + viewport.Right) / 2, high = viewport.Top + (viewport.Bottom - viewport.Top) / 4, low = viewport.Top + 3 * (viewport.Bottom - viewport.Top) / 4;
				var profile = session.Context.Profile;
				await new AdbCommandTransport (profile.AdbExecutable, profile.DeviceSerial, TimeSpan.FromSeconds (25)).ExecuteAsync (["shell", "input", "swipe", Number (x), Number (low), Number (x), Number (high), "350"], token);
				}
			if (!complete || observed.Count != expectedLabels.Length || !observed.TryGetValue (expectedSelected, out bool selected) || !selected)
				throw new InvalidDataException ("The complete expected picker contents were not observed.");
			}
		catch (Exception error) { failure = error; throw; }
		finally
			{
			using var close = new CancellationTokenSource (TimeSpan.FromMinutes (3));
			try
				{
			await session.CaptureAsync (checkId + ".before-close", h => Selection (h), close.Token);
			Intent ("close");
			// Dismiss through the observed in-app Back control.
			await session.Device.TapAsync (CrestronHomePages.Resource ("customdevice_selectionToolbar_backButton"), h => Selection (h), close.Token);
			await session.CaptureAsync (checkId + ".closed", h => Page (h), close.Token);
				}
			catch (Exception cleanupError) when (failure != null)
				{
				throw new AggregateException ("Picker inspection and restoration both failed.", failure, cleanupError);
				}
			}
		}
	private static string Number (int value) => value.ToString (System.Globalization.CultureInfo.InvariantCulture);

	private static async Task<AndroidSelector> Reveal (CrestronHomeExtensionNavigation pages, string text, CancellationToken token)
		{
		foreach (bool down in new[] { true, false })
			{
			var seen = new HashSet<string> (StringComparer.Ordinal);
			for (int i = 0; i < 10; i++)
				{
				AndroidSelector? found = null;
				string signature = "";
				await pages.InspectAsync ("easee.reveal-" + text.Replace (" ", "-") + "-" + down + "-" + i, page =>
					 {
						 var viewport = page.RequireUnique (CrestronHomePages.Resource ("customdevices_componentRecyclerView"));
						 var selector = UiText.TrySelector (page, text);
						 if (selector != null)
							 {
							 var target = page.RequireUnique (selector);
							 if (target.Enabled && target.Top > viewport.Top && target.Bottom < viewport.Bottom)
								 found = selector;
							 }
						 signature = page.MaskedXml;
					 }, token);
				if (found != null)
					return found;
				if (!seen.Add (signature))
					break;
				if (down)
					await pages.ScrollDownAsync (_ => { }, token);
				else
					await pages.ScrollUpAsync (_ => { }, token);
				}
			}
		throw new InvalidDataException ("Required navigation control was not fully visible.");
		}
	}