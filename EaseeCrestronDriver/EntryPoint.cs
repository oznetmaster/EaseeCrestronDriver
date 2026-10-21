using Crestron.DeviceDrivers.EntityModel;
using Crestron.DeviceDrivers.SDK;
using Crestron.DeviceDrivers.SDK.EntityModel;

using Easee.CrestronDriver;

[assembly: DriverAssemblyEntryPoint (typeof (EntryPoint))]

namespace Easee.CrestronDriver;

/// <summary>Creates the Easee Entity V2 driver and its configuration controller.</summary>
public sealed class EntryPoint : DriverAssemblyEntryPoint
	{
	/// <inheritdoc/>
	public override DriverController CreateDriverControllerInstance (DriverControllerCreationArgs args)
		{
		var resources = DriverImplementationResources.FromCreationArgs (args, typeof (EntryPoint));
		var driver = new EaseeDriver (args, resources);
		return new DispatchingDeviceController (new ConfigurableDriverEntity (driver.ControllerId, driver, driver.ConfigurationController), args, null);
		}
	}