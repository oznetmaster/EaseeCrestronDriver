using Crestron.DeviceDrivers.EntityModel.Data;
using Crestron.DeviceDrivers.SDK.EntityModel.Attributes;

namespace Easee.CrestronDriver;

public sealed partial class EaseeDriver
	{
	/// <summary>Gets whether the driver can communicate with Easee, including account discovery before charger selection.</summary>
	[EntityProperty (Id = "onlineIndicator:isOnline")]
	public bool OnlineIndicatorIsOnline
		{
		get; private set => Update ("onlineIndicator:isOnline", value, ref field);
		}
	/// <summary>Gets whether the driver is ready for selection navigation or reporting a selected charger.</summary>
	[EntityProperty (Id = "readyIndicator:isReady")]
	public bool ReadyIndicatorIsReady
		{
		get; private set => Update ("readyIndicator:isReady", value, ref field);
		}
	/// <summary>Gets the current extension UI definition, including the saved tile placement.</summary>
	[EntityProperty (Id = "extension:uiDefinition")]
	public string ExtensionUiDefinition { get; private set => Update ("extension:uiDefinition", value, ref field); } = "";
	/// <summary>Gets the room tile status and current charging power or next scheduled start.</summary>
	[EntityProperty (Id = "tileDisplay")]
	[EntityPropertyMetadata (ExtensionUiProperty = true, Programmable = false)]
	public string TileDisplay { get; private set => Update ("tileDisplay", value, ref field); } = "Configure the Easee account";

	/// <summary>Gets the room tile icon for the observed charger state.</summary>
	[EntityProperty (Id = "tileIcon")]
	[EntityPropertyMetadata (ExtensionUiProperty = true, Programmable = false)]
	public string TileIcon { get; private set => Update ("tileIcon", value, ref field); } = "icInfo";

	/// <summary>Gets the reported charger state; unknown data is explicitly unavailable.</summary>
	[EntityProperty (Id = "status")]
	[EntityPropertyMetadata (ExtensionUiProperty = true, Programmable = true)]
	public string Status { get; private set => Update ("status", value, ref field); } = "Waiting for configuration";

	/// <summary>Gets the current connection and recovery state.</summary>
	[EntityProperty (Id = "connectionStatus")]
	[EntityPropertyMetadata (ExtensionUiProperty = true, Programmable = true)]
	public string ConnectionStatus { get; private set => Update ("connectionStatus", value, ref field); } = "Waiting for configuration";

	/// <summary>Gets the latest command request outcome, separate from actual charging state.</summary>
	[EntityProperty (Id = "commandStatus")]
	[EntityPropertyMetadata (ExtensionUiProperty = true, Programmable = true)]
	public string CommandStatus { get; private set => Update ("commandStatus", value, ref field); } = "";

	/// <summary>Gets whether the selected charger is reported connected to Easee cloud.</summary>
	[EntityProperty (Id = "online")]
	[EntityPropertyMetadata (ExtensionUiProperty = true, Programmable = true)]
	public bool Online { get; private set => Update ("online", value, ref field); } = false;

	/// <summary>Gets whether the driver currently has a usable charger connection.</summary>
	[EntityProperty (Id = "controlsEnabled")]
	[EntityPropertyMetadata (ExtensionUiProperty = true, Programmable = false)]
	public bool ControlsEnabled { get; private set => Update ("controlsEnabled", value, ref field); } = false;

	/// <summary>Gets whether current telemetry permits requesting immediate charging.</summary>
	[EntityProperty (Id = "canCharge")]
	[EntityPropertyMetadata (ExtensionUiProperty = true, Programmable = true)]
	public bool CanCharge { get; private set => Update ("canCharge", value, ref field); } = false;

	/// <summary>Gets whether current telemetry permits stopping charging.</summary>
	[EntityProperty (Id = "canStopCharging")]
	[EntityPropertyMetadata (ExtensionUiProperty = true, Programmable = true)]
	public bool CanStopCharging { get; private set => Update ("canStopCharging", value, ref field); } = false;

	/// <summary>Gets whether a vehicle is confirmed connected; consult ConnectionKnown.</summary>
	[EntityProperty (Id = "vehicleConnected")]
	[EntityPropertyMetadata (ExtensionUiProperty = true, Programmable = true)]
	public bool VehicleConnected { get; private set => Update ("vehicleConnected", value, ref field); } = false;

	/// <summary>Gets whether vehicle connection is known from the current charger mode.</summary>
	[EntityProperty (Id = "connectionKnown")]
	[EntityPropertyMetadata (ExtensionUiProperty = true, Programmable = true)]
	public bool ConnectionKnown { get; private set => Update ("connectionKnown", value, ref field); } = false;

	/// <summary>Gets whether the charger reports its charging operation mode.</summary>
	[EntityProperty (Id = "charging")]
	[EntityPropertyMetadata (ExtensionUiProperty = true, Programmable = true)]
	public bool Charging { get; private set => Update ("charging", value, ref field); } = false;

	/// <summary>Gets charging power in kilowatts; consult PowerKnown before using this value.</summary>
	[EntityProperty (Id = "powerKilowatts")]
	[EntityPropertyMetadata (ExtensionUiProperty = true, Programmable = true)]
	public double PowerKilowatts { get; private set => Update ("powerKilowatts", value, ref field); } = 0;

	/// <summary>Gets whether charging power is available.</summary>
	[EntityProperty (Id = "powerKnown")]
	[EntityPropertyMetadata (ExtensionUiProperty = true, Programmable = true)]
	public bool PowerKnown { get; private set => Update ("powerKnown", value, ref field); } = false;

	/// <summary>Gets charging power with units, or an unavailable marker.</summary>
	[EntityProperty (Id = "powerDisplay")]
	[EntityPropertyMetadata (ExtensionUiProperty = true, Programmable = false)]
	public string PowerDisplay { get; private set => Update ("powerDisplay", value, ref field); } = "--";

	/// <summary>Gets reported session energy in kilowatt hours; consult SessionEnergyKnown.</summary>
	[EntityProperty (Id = "sessionEnergy")]
	[EntityPropertyMetadata (ExtensionUiProperty = true, Programmable = true)]
	public double SessionEnergy { get; private set => Update ("sessionEnergy", value, ref field); } = 0;

	/// <summary>Gets whether session energy is available.</summary>
	[EntityProperty (Id = "sessionEnergyKnown")]
	[EntityPropertyMetadata (ExtensionUiProperty = true, Programmable = true)]
	public bool SessionEnergyKnown { get; private set => Update ("sessionEnergyKnown", value, ref field); } = false;

	/// <summary>Gets session energy with units, or an unavailable marker.</summary>
	[EntityProperty (Id = "sessionEnergyDisplay")]
	[EntityPropertyMetadata (ExtensionUiProperty = true, Programmable = false)]
	public string SessionEnergyDisplay { get; private set => Update ("sessionEnergyDisplay", value, ref field); } = "--";

	/// <summary>Gets reported lifetime energy in kilowatt hours.</summary>
	[EntityProperty (Id = "lifetimeEnergyDisplay")]
	[EntityPropertyMetadata (ExtensionUiProperty = true, Programmable = false)]
	public string LifetimeEnergyDisplay { get; private set => Update ("lifetimeEnergyDisplay", value, ref field); } = "--";

	/// <summary>Gets whether the cable is physically locked; consult CableLockKnown.</summary>
	[EntityProperty (Id = "cableLocked")]
	[EntityPropertyMetadata (ExtensionUiProperty = true, Programmable = true)]
	public bool CableLocked { get; private set => Update ("cableLocked", value, ref field); } = false;

	/// <summary>Gets whether physical cable lock status is known.</summary>
	[EntityProperty (Id = "cableLockKnown")]
	[EntityPropertyMetadata (ExtensionUiProperty = true, Programmable = true)]
	public bool CableLockKnown { get; private set => Update ("cableLockKnown", value, ref field); } = false;

	/// <summary>Gets reported permanent cable-lock setting; consult CableSettingKnown.</summary>
	[EntityProperty (Id = "keepCableLocked")]
	[EntityPropertyMetadata (ExtensionUiProperty = true, Programmable = true)]
	public bool KeepCableLocked { get; private set => Update ("keepCableLocked", value, ref field); } = false;

	/// <summary>Gets whether the permanent cable-lock setting is known.</summary>
	[EntityProperty (Id = "cableSettingKnown")]
	[EntityPropertyMetadata (ExtensionUiProperty = true, Programmable = true)]
	public bool CableSettingKnown { get; private set => Update ("cableSettingKnown", value, ref field); } = false;

	/// <summary>Gets the reported dynamic charger current limit in amperes.</summary>
	[EntityProperty (Id = "currentLimitDisplay")]
	[EntityPropertyMetadata (ExtensionUiProperty = true, Programmable = false)]
	public string CurrentLimitDisplay { get; private set => Update ("currentLimitDisplay", value, ref field); } = "--";

	/// <summary>Gets the next verified schedule start in its published time zone.</summary>
	[EntityProperty (Id = "nextStartDisplay")]
	[EntityPropertyMetadata (ExtensionUiProperty = true, Programmable = true)]
	public string NextStartDisplay { get; private set => Update ("nextStartDisplay", value, ref field); } = "--";

	/// <summary>Gets the name of the explicitly selected site.</summary>
	[EntityProperty (Id = "siteName")]
	[EntityPropertyMetadata (ExtensionUiProperty = true, Programmable = true)]
	public string SiteName { get; private set => Update ("siteName", value, ref field); } = "--";

	/// <summary>Gets the name of the explicitly selected charger.</summary>
	[EntityProperty (Id = "chargerName")]
	[EntityPropertyMetadata (ExtensionUiProperty = true, Programmable = true)]
	public string ChargerName { get; private set => Update ("chargerName", value, ref field); } = "--";

	/// <summary>Gets the available sites for the configured account.</summary>
	[EntityProperty (Id = "siteOptions", Type = DriverEntityValueType.Array, ItemType = DriverEntityValueType.AvailableValue)]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public DriverEntityAvailableValue[] SiteOptions { get; private set; } = [];
	/// <summary>Gets the explicitly selected site identifier.</summary>
	[EntityProperty (Id = "selectedSite", Type = DriverEntityValueType.String, AvailableValuesProperty = "siteOptions")]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public string SelectedSite { get; private set => Update ("selectedSite", value, ref field); } = "";
	/// <summary>Gets the chargers belonging to the selected site.</summary>
	[EntityProperty (Id = "chargerOptions", Type = DriverEntityValueType.Array, ItemType = DriverEntityValueType.AvailableValue)]
	[EntityPropertyMetadata (ExtensionUiProperty = true)]
	public DriverEntityAvailableValue[] ChargerOptions { get; private set; } = [];
	/// <summary>Gets the selected charger serial number.</summary>
	[EntityProperty (Id = "selectedCharger", Type = DriverEntityValueType.String, AvailableValuesProperty = "chargerOptions")]
	[EntityPropertyMetadata (ExtensionUiProperty = true, Programmable = true)]
	public string SelectedCharger { get; private set => Update ("selectedCharger", value, ref field); } = "";
	}