# EaseeCrestronDriver

Crestron Home Entity V2 driver for one Easee charger, using the published **EaseeChargerClient 1.0.0** package. The shipping solution and its unit/live tests target **.NET Framework 4.7.2** and use the latest C# language. The separate Android UI validation harness targets .NET 10 for the public test workflow. [Public source](https://github.com/oznetmaster/EaseeCrestronDriver). Download the stable driver package from [GitHub Releases](https://github.com/oznetmaster/EaseeCrestronDriver/releases/latest).

The tile reports status and charging power. When a vehicle is connected and idle, a verified future schedule start takes precedence. Configure can place the tile on its room page, the home page, or both. The Room tile provides a direct Charge/Stop action and separate detail navigation. The Home tile opens details, where Charge and Stop are available. This matches the observed Crestron Home 4.11.1+pr behavior and the selected design. A shared v3 tile definition retains action and navigation so Both can provide the Room action.

Charge authorizes the connected vehicle and requests an immediate schedule override. Stop deauthorizes the session. HTTP acceptance is reported separately from observed charging. The driver does not estimate battery percentage. SignalR supplies live observations, with a status check every minute and schedule refresh every five minutes or after a schedule notification.

## Prerequisites

- A Crestron Home processor supporting Entity V2 drivers. Validation used Crestron Home 4.11.1+pr.
- An Easee account with access to the intended site and charger, and Internet access from the processor to Easee's cloud services.
- Processor credentials for package import and access to Crestron Home Setup.

## Installation

Use the [Crestron Home Driver Feed Installer](https://github.com/oznetmaster/Crestron-Home-Driver-Feed-Installer) repository and application to download and install a community-distributed release.

If you prefer to install manually, use the attached `.pkg` asset from the relevant [GitHub Release](https://github.com/oznetmaster/EaseeCrestronDriver/releases/latest). The automatic GitHub `Source code (zip)` and `Source code (tar.gz)` assets are repository snapshots, not installable Crestron driver packages.

NuGet package availability: this driver is also distributed as the [`CrestronHomeDriver.Easee.Charger`](https://www.nuget.org/packages/CrestronHomeDriver.Easee.Charger) NuGet package. This package conforms to the **Crestron Home Driver NuGet Publishing Standard v1**. It is a distribution wrapper for the final `.pkg` artifact, includes the required `crestron-driver-package.json` manifest, and is not intended as a direct DLL reference package.

Crestron Home Driver NuGet Publishing Standard v1 is **not** an official Crestron product or specification. It is an open source packaging standard created to facilitate community distribution and discovery of Crestron Home drivers through NuGet.

For this driver, download **EaseeCrestronDriver.pkg**. The separate `EaseeCrestronDriver.ProcessorTests.pkg` is a test host, not the charger driver.

1. Connect to the processor using SFTP and your processor credentials. Upload `EaseeCrestronDriver.pkg` to `/user/ThirdPartyDrivers/Import` and allow the import to finish.
2. Open **Crestron Home Setup**, connect to the processor and open **Pair Devices**. Find **Easee Charger** under manufacturer **Easee**, category **Energy Automation** (developer **Neil Colvin**), and add it to the intended room.
3. Complete the **Easee account** configuration with the account email address or phone number and password. Choose **Room page**, **Home page** or **Both** for tile placement, then save.
4. Open the driver's detail page on the touch panel or Home app, then open **Settings**. Select the **Site**, then the **Charger**, checking its name and serial number. The driver does not choose a charger automatically.
5. Confirm **Connected to Easee**, the intended charger selection, and current status/power. The Room tile provides direct Charge/Stop and separate detail navigation; the Home tile opens the detail page containing those controls.

The password is retained for automatic account recovery. Enter it only in the driver's masked configuration field; do not put account credentials in GitHub issues or shared logs. The processor runs the integration directly; the Windows EaseeChargerClient app is not required.

### Updating an existing installation

Import the new `.pkg`, then apply the available driver update to the existing **Easee Charger** instance in Setup. Importing a package alone does not update the installed instance. Review the account configuration, selected site/charger, room and tile placement after updating, and confirm that status and power are live.

Public releases **1.0.2 and later** use `vehicleConnectedEvent` for the programmable **Vehicle connected** event; the `vehicleConnected` state property is unchanged. Reselect any existing programming reference to the old event ID, then verify the relevant automation. The displayed event name remains **Vehicle connected**.

## Setup and controls

Enter the Easee username and masked password in Configure. The password is retained for automatic fresh sign-in recovery; rotating token sessions are checkpointed in the processor's native datastore. Masking does not claim encryption of the processor's configuration database. Select an account site and then its charger in the Settings page. Lists identify chargers by name and serial number. Changing site clears the charger selection. No charger is selected automatically. The explicit selection is stored separately for this driver instance and account, so reconfiguring the Account step or reloading the driver can restore it. Dynamic pickers publish inline option snapshots for Crestron Home compatibility.

Details include session and lifetime energy, schedule information, connection state and command outcome. Settings expose a temporary current limit with an explicit positive expiry, and permanent charger-side cable lock. Current choices are bounded by reported charger and cable limits and checked again before the write. The physical cable-lock state and permanent lock setting are separate properties.

All exposed properties, commands and events use entity attributes. Programmable events cover charging start/stop, vehicle connection/disconnection, online/offline and charger error. Initial and reconnect snapshots establish a baseline instead of manufacturing charging events. Missing data has explicit availability flags. The `commandActivity` diagnostic property records instance epoch and completed/pending command counts; completion does not imply charging began.

## Build and test

CI and release setup are documented in [PUBLISHING.md](https://github.com/oznetmaster/EaseeCrestronDriver/blob/master/PUBLISHING.md). The shipping solution remains net472; desktop workflow, discovery and Android tools live separately under `validation/`.

Requires Windows, .NET SDK 10, and `dotnet-ilrepack` 2.0.45. ManifestUtil 29.0.10 is restored from NuGet. The processor package consumes the pinned published CrestronHomeNUnit 3.0.0 SDK described in `ProcessorTestSdk.lock.json`.

```powershell
dotnet test EaseeCrestronDriver.slnx -c Debug -p:BuildDriverPackage=false --filter 'TestCategory!=Processor&TestCategory!=Live&TestCategory!=LiveSetup'
dotnet build EaseeCrestronDriver/EaseeCrestronDriver.csproj -c Debug
dotnet build EaseeCrestronDriver.ProcessorTests/EaseeCrestronDriver.ProcessorTests.csproj -c Debug -p:BuildProcessorTestPackages=true -p:ProcessorTestSdkRoot=C:\Path\To\PublishedSdk
```

Debug packaging advances the manifest revision. Packaging merges and patches the complete non-Crestron dependency closure, includes original dependency licence/notice bytes, normalizes ZIP paths for the processor and verifies the packaged assembly and licence hashes. Builds never deploy automatically. The copied model `.editorconfig` applies; compiler and XML documentation warnings are errors.

NUnit 5 suites cover state mapping, unknown data, observation ordering, commands, token recovery, schedule time zones/DST, XML schema/bindings, native SDK lifecycle, datastore and command activity. Separate live tests validate the selected account/site/charger, observations, schedules and SignalR. Processor live inputs contain only an expiring access token and selected IDs. They contain no password or refresh token.

Live start/stop tests require separate explicit enablement. They record intent before execution, issue no automatic control retries and verify a final noncharging state. A full vehicle can remain Completed at zero power; a passing request test does not prove energy delivery. Live current-limit and cable-lock changes have not been authorized or exercised.

The repeatable HPNEIL-only entry point is documented in [validation/Workflow/README.md](https://github.com/oznetmaster/EaseeCrestronDriver/blob/master/validation/Workflow/README.md). It uses the published public SDK for native and pinned installed-driver UI workflows, requires private plans and explicit live/update/start-stop opt-ins, and retains uncertain reservations. Eight build/plan-validation checks passed without entering a hardware workflow.

Use the existing HPNEIL worker and public CrestronHomeNUnit workflow. HPNEIL and the .241/.244 processor pool are shared: acquire the machine reservation and public processor lease, retain uncertain ownership for reconciliation, and verify cleanup before release. Never reroute to SCOTTISHNEIL or change runner labels. Private account files, profiles, initial configuration and test evidence remain outside the checkout.

## Validation status

The current suites contain **54 core and eight contract tests** on desktop, a **70-test native processor suite**, and separate opt-in live/UI suites. The release gates require passing exact-source hosted and processor checks. Public workflows never receive charger credentials.

The connection-ID fix was loaded on the review processor with no new definition errors, preserved selections/placement and continued charging feedback. The user also confirmed actual vehicle charging initiated from the panel. These observations are retained separately from earlier tests with a fully charged car; no energy-delivery claim is retroactively added to those tests.

See [VALIDATION.md](https://github.com/oznetmaster/EaseeCrestronDriver/blob/master/VALIDATION.md) for requirements, candidate identities and limits, [DEVELOPMENT-HISTORY.md](https://github.com/oznetmaster/EaseeCrestronDriver/blob/master/DEVELOPMENT-HISTORY.md) for historical runs, and [MODEL-COMPARISON.md](https://github.com/oznetmaster/EaseeCrestronDriver/blob/master/MODEL-COMPARISON.md) for alignment with the model. Live current-limit and cable-lock writes remain unexercised. Changes to tests or documentation alone can use the separate processor-test release workflow without publishing a new driver.

Easee and Crestron product names belong to their respective owners. This is an independent community driver and is not affiliated with or endorsed by Easee or Crestron.

Copyright Neil Colvin. MIT License with Commons Clause; see LICENSE.
