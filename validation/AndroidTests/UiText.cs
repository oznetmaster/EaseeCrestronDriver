using System.Xml.Linq;

using CrestronHomeNUnit.Android;

namespace Easee.CrestronDriver.AndroidTests;

/// <summary>Resolves display text using ordinal case-insensitive matching before using exact SDK locators.</summary>
internal static class UiText
	{
	private const string Package = "com.crestron.phoenix.app";
	public static AndroidSelector? TrySelector (AndroidHierarchy hierarchy, string text)
		{
		var matches = XDocument.Parse (hierarchy.MaskedXml).Descendants ("node").Where (node =>
			(string?)node.Attribute ("package") == Package &&
			string.Equals ((string?)node.Attribute ("text"), text, StringComparison.OrdinalIgnoreCase)).ToArray ();
		if (matches.Length > 1)
			throw new InvalidDataException ("Expected UI label is ambiguous ignoring case.");
		return matches.Length == 0 ? null : new AndroidSelector (AndroidSelectorKind.Text, (string)matches[0].Attribute ("text")!);
		}
	public static AndroidSelector Selector (AndroidHierarchy hierarchy, string text) =>
		TrySelector (hierarchy, text) ?? throw new InvalidDataException ("Expected UI label is missing.");
	}
