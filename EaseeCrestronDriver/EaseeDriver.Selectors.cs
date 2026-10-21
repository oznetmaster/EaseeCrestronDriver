using Crestron.DeviceDrivers.EntityModel.Data;
using Crestron.DeviceDrivers.SDK.EntityModel;

namespace Easee.CrestronDriver;

public sealed partial class EaseeDriver
	{
	private readonly Dictionary<string, DriverEntityAvailableValue[]> _inlineOptions = new (StringComparer.Ordinal);

	private void InitializeSelectorDefinitions ()
		{
		PublishSelectorDefinition ("selectedSite", SiteOptions);
		PublishSelectorDefinition ("selectedCharger", ChargerOptions);
		PublishSelectorDefinition ("requestedAmps", CurrentOptions);
		}

	private void PublishSelectorDefinition (string id, DriverEntityAvailableValue[] values)
		{
		using (DefinitionLock.Enter ())
			{
			if (_inlineOptions.TryGetValue (id, out var previous) && previous.SequenceEqual (values))
				return;
			// Home's extension bridge does not resolve AvailableValuesProperty into selector options.
			// Keep the attributed property and command contract, publishing an immutable inline snapshot.
			var snapshot = values.Length == 0
				? new[] { new DriverEntityAvailableValue ("", new DriverEntityLocalizedString (id switch
					{
						"selectedSite" => "Select a site",
						"selectedCharger" => "Select a charger",
						_ => "Choose a current limit"
					}, null), true) }
				: values.ToArray ();
			var definition = DriverEntityPropertyDefinition.Create (DriverEntityValueType.String,
				name: new DriverEntityLocalizedString (id, null), availableValues: snapshot);
			AddProperty (this, id, new DelegatePropertyInstance (definition,
				new DriverEntityPropertyMetadata (programmable: id == "selectedCharger", extensionUiProperty: true),
				(instance, _) => new DriverEntityValue (id switch
					{
						"selectedSite" => ((EaseeDriver)instance).SelectedSite,
						"selectedCharger" => ((EaseeDriver)instance).SelectedCharger,
						"requestedAmps" => ((EaseeDriver)instance).RequestedAmps,
						_ => throw new ArgumentOutOfRangeException (nameof (id))
					})));
			_inlineOptions[id] = values.ToArray ();
			}
		RaiseDefinitionChangedEvent ();
		}
	}
