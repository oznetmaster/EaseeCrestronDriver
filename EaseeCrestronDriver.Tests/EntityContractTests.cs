using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;

using Crestron.DeviceDrivers.SDK.EntityModel.Attributes;

namespace Easee.CrestronDriver.Tests;

[TestFixture]
public sealed class EntityContractTests
	{
	private static string Template => Resource ("Ui");
	private static string Resource (string name)
		{
		using var stream = typeof (EntityContractTests).Assembly.GetManifestResourceStream ("EaseeDriver.Tests." + name) ?? throw new InvalidDataException ("Missing test resource.");
		using var reader = new StreamReader (stream);
		return reader.ReadToEnd ();
		}
	[TestCase (TilePlacement.Room, "#false", "#true")]
	[TestCase (TilePlacement.Home, "#true", "#false")]
	[TestCase (TilePlacement.Both, "#true", "#true")]
	public void PlacementPublishesValidLiteralFlagsAndPreservesActionAndNavigation (int placement, string home, string room)
		{
		var document = XDocument.Parse (TileLayout.Render (Template, (TilePlacement)placement));
		var schemas = new XmlSchemaSet ();
		using var schemaReader = XmlReader.Create (new StringReader (Resource ("Schema")));
		schemas.Add (null, schemaReader);
		document.Validate (schemas, (_, error) => Assert.Fail (error.Message));
		var tile = document.Root!.Element ("tile")!;
		Assert.That ((string?)tile.Attribute ("showinhomepage"), Is.EqualTo (home));
		Assert.That ((string?)tile.Attribute ("showinroompage"), Is.EqualTo (room));
		Assert.That ((string?)tile.Attribute ("action"), Is.EqualTo ("command:toggleCharging"));
		Assert.That ((string?)tile.Attribute ("navigation"), Is.EqualTo ("show:MainPage"));
		}
	[Test]
	public void EveryUiBindingResolvesToAnAttributedPropertyOrCommand ()
		{
		var properties = typeof (EaseeDriver).GetProperties ().Select (p => p.GetCustomAttribute<EntityPropertyAttribute> ()?.Id).Where (id => id is not null).ToArray ();
		var methods = typeof (EaseeDriver).GetMethods ();
		var commands = methods.Select (m => m.GetCustomAttribute<EntityCommandAttribute> ()?.Id).Where (id => id is not null).ToArray ();
		foreach (Match match in Regex.Matches (Template, @"\{(\w+)\}"))
			Assert.That (properties, Does.Contain (match.Groups[1].Value));
		foreach (Match match in Regex.Matches (Template, @"command:(\w+)"))
			Assert.That (commands, Does.Contain (match.Groups[1].Value));
		var document = XDocument.Parse (Template);
		foreach (var control in document.Descendants ().Where (e => e.Name.LocalName is "selectorbutton" or "toggle"))
			{
			var property = ((string)control.Attribute ("value")!).Trim ('{', '}');
			Assert.That (commands, Does.Contain ("set" + char.ToUpperInvariant (property[0]) + property.Substring (1)));
			}
		}
	[Test]
	public void PublicDriverPropertiesAndEventsUseAttributes ()
		{
		foreach (var property in typeof (EaseeDriver).GetProperties (BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
			Assert.That (property.GetCustomAttribute<EntityPropertyAttribute> (), Is.Not.Null, property.Name);
		foreach (var driverEvent in typeof (EaseeDriver).GetEvents (BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
			{
			Assert.That (driverEvent.GetCustomAttribute<EntityEventAttribute> (), Is.Not.Null, driverEvent.Name);
			Assert.That (driverEvent.GetCustomAttribute<EntityEventMetadataAttribute> ()?.Programmable, Is.True, driverEvent.Name);
			}
		}
	[Test]
	public void UiTranslationKeysArePresent ()
		{
		var translations = JsonSerializer.Deserialize<Dictionary<string, string>> (Resource ("Translation"))!;
		foreach (Match match in Regex.Matches (Template, @"\^(\w+)"))
			Assert.That (translations.ContainsKey (match.Groups[1].Value), Is.True, match.Value);
		}
	[Test]
	public void SelectorsBindOptionsThroughEntityAttributes ()
		{
		Assert.That (typeof (EaseeDriver).GetProperty (nameof (EaseeDriver.SelectedSite))!.GetCustomAttribute<EntityPropertyAttribute> ()!.AvailableValuesProperty, Is.EqualTo ("siteOptions"));
		Assert.That (typeof (EaseeDriver).GetProperty (nameof (EaseeDriver.SelectedCharger))!.GetCustomAttribute<EntityPropertyAttribute> ()!.AvailableValuesProperty, Is.EqualTo ("chargerOptions"));
		}
	}