using System.Globalization;

using Crestron.DeviceDrivers.EntityModel.Data;
using Crestron.DeviceDrivers.SDK.EntityModel.Attributes;

namespace Easee.CrestronDriver;

public sealed partial class EaseeDriver
	{
	/// <summary>Requests immediate charging for the connected car, overriding its schedule for this session.</summary>
	[EntityCommand (Id = "startCharging")]
	[EntityCommandMetadata (Programmable = true)]
	public void StartCharging () => Enqueue ((commands, token) => commands.StartAsync (token));
	/// <summary>Requests charging stop and deauthorization for the connected car.</summary>
	[EntityCommand (Id = "stopCharging")]
	[EntityCommandMetadata (Programmable = true)]
	public void StopCharging () => Enqueue ((commands, token) => commands.StopAsync (token));
	/// <summary>Requests start or stop according to freshly read charger state.</summary>
	[EntityCommand (Id = "toggleCharging")]
	[EntityCommandMetadata (Programmable = true)]
	public void ToggleCharging () => Enqueue ((commands, token) => commands.ToggleAsync (token));
	/// <summary>Requests a temporary charger current limit with an explicit expiry.</summary>
	/// <param name="amps">Nonnegative whole amperes, bounded by reported charger and cable limits; zero pauses current delivery.</param>
	/// <param name="minutes">Positive duration in minutes; this command never requests an indefinite limit.</param>
	[EntityCommand (Id = "setTemporaryCurrentLimit")]
	[EntityCommandMetadata (Programmable = true)]
	public void SetTemporaryCurrentLimit ([EntityParameter (Id = "amps")] int amps, [EntityParameter (Id = "minutes")] int minutes)
		 => Enqueue ((commands, token) => commands.SetCurrentAsync (amps, minutes, token));
	/// <summary>Requests the permanent cable-lock setting; disabling it releases the cable only after disconnection.</summary>
	/// <param name="value">True keeps the charger-side cable permanently locked.</param>
	[EntityCommand (Id = "setKeepCableLocked")]
	[EntityCommandMetadata (Programmable = true)]
	public void SetKeepCableLocked ([EntityParameter (Id = "value")] bool value)
		 => Enqueue ((commands, token) => commands.SetCableLockAsync (value, token));
	/// <summary>Requests a status refresh without changing charger settings.</summary>
	[EntityCommand (Id = "refreshNow")]
	[EntityCommandMetadata (Programmable = true)]
	public void RefreshNow ()
		{
		if (_wake.CurrentCount == 0)
			try
				{
				_wake.Release ();
				}
			catch (SemaphoreFullException) { }
		}
	/// <summary>Selects a site from the configured account and clears the previous charger selection.</summary>
	/// <param name="value">A site identifier from the current site picker.</param>
	[EntityCommand (Id = "setSelectedSite")]
	public void SetSelectedSite ([EntityParameter (Id = "value")] string value)
		{
		if (!_sites.Any (s => s.Id?.ToString (CultureInfo.InvariantCulture) == value))
			{
			CommandStatus = "Select an available site";
			return;
			}
		if (value == SelectedSite)
			return;
		SaveSelection (value, "");
		ChargerOptions = [];
		PublishSelectorDefinition ("selectedCharger", ChargerOptions);
		NotifyPropertyChanged ("chargerOptions", new DriverEntityValue (ChargerOptions));
		_chargers = [];
		Restart (_username, _password, value, "");
		}
	/// <summary>Selects a charger belonging to the selected site.</summary>
	/// <param name="value">A charger serial number from the current charger picker.</param>
	[EntityCommand (Id = "setSelectedCharger")]
	public void SetSelectedCharger ([EntityParameter (Id = "value")] string value)
		{
		if (!_chargers.Any (c => string.Equals (c.Id, value, StringComparison.OrdinalIgnoreCase)))
			{
			CommandStatus = "Select an available charger";
			return;
			}
		if (value == SelectedCharger)
			return;
		SaveSelection (SelectedSite, value);
		Restart (_username, _password, SelectedSite, value);
		}
	private void SaveSelection (string site, string charger)
		{
		new ProcessorSelectionStore (_creation.DriverId, _username).Save (site, charger);
		ConfigurationController.NotifyValuesChanged (new Dictionary<string, DriverEntityValue?>
			{
			["SiteId"] = new DriverEntityValue (site),
			["ChargerId"] = new DriverEntityValue (charger)
			});
		}
	private void PublishSiteOptions ()
		{
		SiteOptions = _sites.Where (s => s.Id.HasValue).Select (s =>
			 {
				 var id = s.Id!.Value.ToString (CultureInfo.InvariantCulture);
				 var label = (s.Name ?? "Site") + " (" + id + ")";
				 return new DriverEntityAvailableValue (id, new DriverEntityLocalizedString (label, label), false);
			 }).ToArray ();
		PublishSelectorDefinition ("selectedSite", SiteOptions);
		NotifyPropertyChanged ("siteOptions", new DriverEntityValue (SiteOptions));
		}
	private void PublishChargerOptions ()
		{
		ChargerOptions = _chargers.Where (c => !string.IsNullOrWhiteSpace (c.Id)).Select (c =>
			 {
				 var label = (c.Name ?? "Charger") + " (" + c.Id + ")";
				 return new DriverEntityAvailableValue (c.Id!, new DriverEntityLocalizedString (label, label), false);
			 }).ToArray ();
		PublishSelectorDefinition ("selectedCharger", ChargerOptions);
		NotifyPropertyChanged ("chargerOptions", new DriverEntityValue (ChargerOptions));
		}
	}
