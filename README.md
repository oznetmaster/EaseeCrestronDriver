# EaseeCrestronDriver

Crestron Home Entity V2 driver for one Easee charger, using the published **EaseeChargerClient 1.0.0** package. The shipping solution and its unit/live tests target **.NET Framework 4.7.2** and use the latest C# language. The separate Android UI validation harness targets .NET 10 for the public test workflow. [Public source](https://github.com/oznetmaster/EaseeCrestronDriver). No public driver release has been published.

The tile reports status and charging power. When a vehicle is connected and idle, a verified future schedule start takes precedence. Configure can place the tile on its room page, the home page, or both. The Room tile provides a direct Charge/Stop action and separate detail navigation. The Home tile opens details, where Charge and Stop are available. This matches the observed Crestron Home 4.11.1+pr behavior and the selected design. A shared v3 tile definition retains action and navigation so Both can provide the Room action.

Charge authorizes the connected vehicle and requests an immediate schedule override. Stop deauthorizes the session. HTTP acceptance is reported separately from observed charging. The driver does not estimate battery percentage. SignalR supplies live observations, with a status check every minute and schedule refresh every five minutes or after a schedule notification.

## Setup and controls

Enter the Easee username and masked password in Configure. The password is retained for automatic fresh sign-in recovery; rotating token sessions are checkpointed in the processor's native datastore. Masking does not claim encryption of the processor's configuration database. Select an account site and then its charger in the Settings page. Lists identify chargers by name and serial number. Changing site clears the charger selection. No charger is selected automatically. The explicit selection is stored separately for this driver instance and account, so reconfiguring the Account step or reloading the driver can restore it. Dynamic pickers publish inline option snapshots for Crestron Home compatibility.

Details include session and lifetime energy, schedule information, connection state and command outcome. Settings expose a temporary current limit with an explicit positive expiry, and permanent charger-side cable lock. Current choices are bounded by reported charger and cable limits and checked again before the write. The physical cable-lock state and permanent lock setting are separate properties.

All exposed properties, commands and events use entity attributes. Programmable events cover charging start/stop, vehicle connection/disconnection, online/offline and charger error. Initial and reconnect snapshots establish a baseline instead of manufacturing charging events. Missing data has explicit availability flags. The `commandActivity` diagnostic property records instance epoch and completed/pending command counts; completion does not imply charging began.

## Build and test

CI and release setup are documented in [PUBLISHING.md](PUBLISHING.md). The shipping solution remains net472; desktop workflow, discovery and Android tools live separately under alidation/.

Requires Windows, .NET SDK 10, and `dotnet-ilrepack` 2.0.45. ManifestUtil 29.0.10 is restored from NuGet. The processor package consumes the pinned published CrestronHomeNUnit 3.0.0 SDK described in `ProcessorTestSdk.lock.json`.

```powershell
dotnet test EaseeCrestronDriver.slnx -c Debug -p:BuildDriverPackage=false --filter 'TestCategory!=Processor&TestCategory!=Live&TestCategory!=LiveSetup'
dotnet build EaseeCrestronDriver/EaseeCrestronDriver.csproj -c Debug
dotnet build EaseeCrestronDriver.ProcessorTests/EaseeCrestronDriver.ProcessorTests.csproj -c Debug -p:BuildProcessorTestPackages=true -p:ProcessorTestSdkRoot=C:\Path\To\PublishedSdk
```

Debug packaging advances the manifest revision. Packaging merges and patches the complete non-Crestron dependency closure, includes original dependency licence/notice bytes, normalizes ZIP paths for the processor and verifies the packaged assembly and licence hashes. Builds never deploy automatically. The copied model `.editorconfig` applies; compiler and XML documentation warnings are errors.

NUnit 5 suites cover state mapping, unknown data, observation ordering, commands, token recovery, schedule time zones/DST, XML schema/bindings, native SDK lifecycle, datastore and command activity. Separate live tests validate the selected account/site/charger, observations, schedules and SignalR. Processor live inputs contain only an expiring access token and selected IDs. They contain no password or refresh token.

Live start/stop tests require separate explicit enablement. They record intent before execution, issue no automatic control retries and verify a final noncharging state. A full vehicle can remain Completed at zero power; a passing request test does not prove energy delivery. Live current-limit and cable-lock changes have not been authorized or exercised.

The repeatable HPNEIL-only entry point is documented in [validation/Workflow/README.md](validation/Workflow/README.md). It uses the published public SDK for native and pinned installed-driver UI workflows, requires private plans and explicit live/update/start-stop opt-ins, and retains uncertain reservations. Eight build/plan-validation checks passed without entering a hardware workflow.

Use the existing HPNEIL worker and public CrestronHomeNUnit workflow. HPNEIL and the .241/.244 processor pool are shared: acquire the machine reservation and public processor lease, retain uncertain ownership for reconciliation, and verify cleanup before release. Never reroute to SCOTTISHNEIL or change runner labels. Private account files, profiles, initial configuration and test evidence remain outside the checkout.

## Validation status

The requirement-by-requirement review is in [VALIDATION.md](VALIDATION.md). Before using the retained candidate, run `./scripts/Verify-ValidatedCandidate.ps1` in PowerShell 7. It checks current production source and retained package/evidence identity without rebuilding or contacting hardware; any source drift needs fresh candidate validation.

On 21 October 2026, candidate **0.1.000.0006** passed the following checks on HPNEIL and the .241 processor in Office:

| Validation | Passed |
| --- | ---: |
| Desktop NUnit tests | 61 |
| Processor NUnit tests | 69 |
| Read-only processor live tests | 4 |
| Installed-driver checks | 6 |
| Actual-app UI scenarios | 4 |

The UI scenarios separately covered **Room page**, **Home page**, **Both**, and read-only site/charger picker inspection in Crestron Home 4.11.1+pr. Placement checks verified presence/absence, readable status and power, navigation, authorized start/stop requests and final noncharging state. Home navigation was checked to issue no charger command. Picker checks verified the complete expected option lists and selected states without selecting an option. Every successful run confirmed Home restoration, the unchanged candidate payload, cleanup and reservation release. The selected site and charger were preserved, and Room-only placement was restored.

The exact package SHA256 is `09d94c09572869d8919683cfe4bfc5a985b383276b498b468e88fb7a3306cb7b`. The local manifest was synchronized from the exact embedded resource in that package. The implementation files match the tested production source. The validated package and retained evidence are staged in `artifacts/validated/0.1.000.0006/`. Debug packaging advances the revision; rebuilding does not reproduce this already validated package automatically.

Earlier failed and blocked runs remain failed results. Cleanup is documented in their workflow results or separate reconciliation receipts. During build investigation, a previously blocked driver DLL later passed unchanged. A UI binary remained blocked in controlled and crossed-folder comparisons. Normalized source paths now make the UI harness builds reproducible across source directories. The final unsigned UI assembly ran with Smart App Control enforced; no signing certificate or security-policy change was used. The cause of Windows' earlier differing verdicts remains unproved.

Start/stop request acceptance with the fully charged vehicle does not demonstrate energy delivery. Live current-limit and cable-lock writes remain unexercised. The source repository is public; the validated development package has no public release tag. See [CHANGELOG.md](CHANGELOG.md) and [RELEASE-NOTES.md](RELEASE-NOTES.md) for the local candidate notes. See [CHANGELOG.md](CHANGELOG.md) and [RELEASE-NOTES.md](RELEASE-NOTES.md) for the local candidate notes.

Copyright Neil Colvin. MIT License with Commons Clause; see LICENSE.
