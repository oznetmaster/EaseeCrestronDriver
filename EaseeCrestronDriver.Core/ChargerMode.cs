namespace Easee.CrestronDriver;

// Observation 109 values published at https://developer.easee.com/docs/enumerations.
// The REST state DTO enum omits 0, 7 and 8; observations carry integer values.
internal enum ChargerMode
	{
	Offline = 0, Disconnected = 1, AwaitingStart = 2, Charging = 3,
	Completed = 4, Error = 5, ReadyToCharge = 6, AwaitingAuthentication = 7, Deauthenticating = 8
	}