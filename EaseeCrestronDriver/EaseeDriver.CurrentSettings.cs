using System.Globalization;

using Crestron.DeviceDrivers.EntityModel.Data;
using Crestron.DeviceDrivers.SDK.EntityModel.Attributes;

namespace Easee.CrestronDriver;

public sealed partial class EaseeDriver
	{
	private int _lastMaximumCurrent = -1;
	/// <summary>Gets the temporary current choices bounded by the reported charger and cable limits.</summary>
	[EntityProperty (Id = "currentOptions", Type = DriverEntityValueType.Array, ItemType = DriverEntityValueType.AvailableValue)]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public DriverEntityAvailableValue[] CurrentOptions { get; private set; } = [];
	/// <summary>Gets the selected temporary current, which is applied only by the explicit Apply command.</summary>
	[EntityProperty (Id = "requestedAmps", Type = DriverEntityValueType.String, AvailableValuesProperty = "currentOptions")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string RequestedAmps { get; private set => Update ("requestedAmps", value, ref field); } = "";
	/// <summary>Gets the selected expiry in minutes, which is applied only by the explicit Apply command.</summary>
	[EntityProperty (Id = "requestedMinutes", AvailableValues = new[] { "5", "15", "30", "60", "120" })]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string RequestedMinutes { get; private set => Update ("requestedMinutes", value, ref field); } = "";
	/// <summary>Selects a current value without sending a charger command.</summary>
	/// <param name="value">A whole-amp value from the current options.</param>
	[EntityCommand (Id = "setRequestedAmps")]
	public void SetRequestedAmps ([EntityParameter (Id = "value")] string value)
		{
		if (int.TryParse (value, NumberStyles.None, CultureInfo.InvariantCulture, out var amps) && amps >= 0 && amps <= _lastMaximumCurrent)
			RequestedAmps = value;
		}
	/// <summary>Selects a positive expiry in minutes without sending a charger command.</summary>
	/// <param name="value">One of the displayed duration choices.</param>
	[EntityCommand (Id = "setRequestedMinutes")]
	public void SetRequestedMinutes ([EntityParameter (Id = "value")] string value)
		{
		if (new[] { "5", "15", "30", "60", "120" }.Contains (value))
			RequestedMinutes = value;
		}
	/// <summary>Applies the explicitly selected current and expiry as a temporary limit.</summary>
	[EntityCommand (Id = "applyTemporaryCurrentLimit")]
	public void ApplyTemporaryCurrentLimit ()
		{
		if (!int.TryParse (RequestedAmps, NumberStyles.None, CultureInfo.InvariantCulture, out int amps) || !int.TryParse (RequestedMinutes, NumberStyles.None, CultureInfo.InvariantCulture, out int minutes))
			{
			CommandStatus = "Choose a current limit and duration first";
			return;
			}
		SetTemporaryCurrentLimit (amps, minutes);
		}
	private void PublishCurrentOptions (ChargerReading reading)
		{
		double? limit = reading.MaximumCurrentAmps;
		if (reading.CableRatingAmps is > 0 && limit.HasValue)
			limit = Math.Min (limit.Value, reading.CableRatingAmps.Value);
		// Reject malformed limits rather than constructing an unbounded selector.
		int maximum = limit is >= 0 and <= 1000 ? (int)Math.Floor (limit.Value) : -1;
		if (maximum == _lastMaximumCurrent)
			return;
		_lastMaximumCurrent = maximum;
		CurrentOptions = maximum < 0 ? [] : Enumerable.Range (0, maximum + 1).Select (amps =>
			 {
				 var value = amps.ToString (CultureInfo.InvariantCulture);
				 var label = value + " A";
				 return new DriverEntityAvailableValue (value, new DriverEntityLocalizedString (label, label), false);
			 }).ToArray ();
		RequestedAmps = "";
		PublishSelectorDefinition ("requestedAmps", CurrentOptions);
		NotifyPropertyChanged ("currentOptions", new DriverEntityValue (CurrentOptions));
		}
	}