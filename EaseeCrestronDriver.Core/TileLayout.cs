using System.Xml.Linq;

namespace Easee.CrestronDriver;

internal enum TilePlacement
	{
	Room, Home, Both
	}
internal static class TileLayout
	{
	internal static TilePlacement Parse (string? value) => value switch
		{
			null or "" or "Room page" => TilePlacement.Room,
			"Home page" => TilePlacement.Home,
			"Both" => TilePlacement.Both,
			_ => throw new ArgumentException ("Choose Room page, Home page or Both.", nameof (value))
			};
	internal static string Render (string template, TilePlacement placement)
		{
		var document = XDocument.Parse (template);
		var tile = document.Root?.Element ("tile") ?? throw new InvalidDataException ("The UI definition has no tile.");
		tile.SetAttributeValue ("showinhomepage", placement is TilePlacement.Home or TilePlacement.Both ? "#true" : "#false");
		tile.SetAttributeValue ("showinroompage", placement is TilePlacement.Room or TilePlacement.Both ? "#true" : "#false");
		return document.ToString (SaveOptions.DisableFormatting);
		}
	}