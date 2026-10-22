# EaseeCrestronDriver 1.0.2

Fixes Crestron Home programming-definition errors caused by the connection-state property and connection event sharing the `vehicleConnected` ID. The property keeps its existing ID; the event uses `vehicleConnectedEvent` with the same “Vehicle connected” display name. Reselect any programming reference to the old event ID.

This release adds the model's NuGet distribution as **CrestronHomeDriver.Easee.Charger**, with the required `CrestronHomeDriver` package type, root `crestron-driver-package.json`, exactly one final Release `.pkg`, required metadata/readme/licence and no library DLL payload or dependencies. The community **Crestron Home Driver NuGet Publishing Standard v1** is not an official Crestron specification.

The README now includes the Crestron Home Driver Feed Installer, alternative manual import instructions, account/site/charger setup and upgrade guidance. Release automation uses NuGet trusted publishing with GitHub OIDC and promotes the exact staged assets after candidate validation. The model comparison also covers the NUnit policy, independent processor-test releases, post-publication payload validation and complete test-host dependency notices.

The collision regression fails against the old event ID and passes after correction. The corrected suites include 62 desktop and 70 processor tests. The installed 1.0.1 development fix loaded without new definition errors while charging feedback remained active; the audited 1.0.2 candidate has its own retained package identity and validation. The user's panel-initiated actual charging observation is human end-to-end evidence, distinct from earlier fully charged-car request tests. No charger commands are issued by publication. Live current-limit and cable-lock writes remain unexercised.

Install **EaseeCrestronDriver.pkg**, directly or through the NuGet/Feed Installer distribution. The processor test package is separate tooling. GitHub includes the same driver bytes, documentation, source identity and SHA256 checksums. Driver version **1.0.002.0000**. Copyright Neil Colvin; MIT License with Commons Clause.
