# EaseeCrestronDriver 1.0.1

Fixes Crestron Home programming-definition errors caused by the connection-state property and connection event sharing the `vehicleConnected` identifier. The property keeps its existing ID; the event now uses `vehicleConnectedEvent` with the same “Vehicle connected” display name. Any programming reference to the old connection-event identifier must be reselected. Other property, command and event IDs are unchanged.

A contract regression checks identifier uniqueness across properties, commands and events, including case-only collisions. Validation reproduces the collision against the old event ID, then runs the corrected desktop and processor suites. Exact-source hosted and App-reported processor checks gate publication. Installed validation checks the Home programming definition and new log entries; it issues no charger commands.

The user also confirmed actual vehicle charging initiated from the driver's touch-panel Charge command on 22 October. This is human-observed end-to-end evidence, separate from the automated tests with a fully charged vehicle. The touch panel recovered after its own reboot; no causal connection between that incident and this definition error has been established.

Install EaseeCrestronDriver.pkg. The release also includes the processor-test package, documentation ZIP, source identity and SHA256 checksums. Driver version: 1.0.001.0000. Copyright Neil Colvin; MIT License with Commons Clause.
