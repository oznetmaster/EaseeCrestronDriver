# Repeatable driver validation

This standalone .NET 10 console is desktop tooling outside the net472 driver solution. It calls the **published CrestronHomeNUnit 3.0.0 public workflow API**, matching `ProcessorTestSdk.lock.json`. It neither forks that SDK nor implements a second processor test runner.

`Invoke-Validation.ps1` is the entry point on **HPNEIL**. It reserves the shared machine before building, rejects active worker/build/test processes, and delegates processor/Android reservations to the public SDK. It never steals another reservation or changes GitHub runner labels. The console additionally requires proof of the launcher's machine reservation. Private plans, processor credentials, Android profiles, live inputs and results must remain outside the checkout.

## Native workflow

Create the private workflow plan using the public `WorkflowPlan` schema. Its required stages are both desktop projects (at least 54 core and eight contract tests), the `easee-driver` processor suite (at least 70), and temporary test-instance/package cleanup. Use the pinned SDK source for `ProcessorTestSdkRoot`. Set the explicit available processor address and verified HTTPS/SSH fingerprints from the protected pool templates; no processor is selected by this tool. The public lease acquisition is authoritative if availability changes after selection.

Optional live validation is the `easee-driver-live` read-only suite (at least four tests). Its inputs are fresh `LiveTestSettings.json` containing only `AccessToken`, `ExpiresAt`, `SiteId`, and `ChargerId`. The existing `IndependentSessionTests` preparation uses an encrypted `Account.dpapi` outside the checkout. The processor receives neither the password nor the refresh chain. Preparing an independent session requires its existing explicit `EASEE_DRIVER_PREPARE_SESSION` and private-directory settings.

Updating the actual driver additionally requires a precise `ActualDriver` target and the six installed checks used for account configuration, navigation readiness, account communication, selected charger online, selected site and selected charger. Preserve existing configuration unless an explicitly reviewed private plan supplies initial configuration. Driver packaging advances the Debug revision; a new package requires new evidence. This workflow does not replace the frozen validated package.

Processor settings JSON contains `UserName` and `Password` from the existing protected worker settings. The launcher passes its file path, never credential values on the command line. Execute an explicitly reviewed plan:

```powershell
$env:ProcessorTestSdkRoot = 'C:\Path\To\PublishedSdkAtTheLockedRevision'
./validation/Workflow/Invoke-Validation.ps1 -Mode Native -PlanPath C:\Private\native-plan.json -SettingsPath C:\Private\processor-settings.json -ResultsDirectory C:\Private\Runs\unique-native-run
```

Add `-AllowLive` when the plan includes the read-only live suite. Add `-AllowDriverUpdate` only when the plan intentionally builds and updates the actual driver. Reboots, native deployed-control plans and native Android plans are rejected by this entry point. Physical UI tests use the separate installed-candidate mode below.

## Installed candidate and Android scenarios

Use the public `InstalledDriverTestPlan` schema. It pins the existing device/parent/catalogue/room/version and candidate package SHA256, with the existing private Android profile and fixture source root. This mode does not deploy or reload the driver. Run **one exact test per private plan**, each with its own retained outcome:

| Scenario | Exact required test |
| --- | --- |
| Room only | `Easee.CrestronDriver.AndroidTests.PlacementUiTests.RoomPageTileIsVisibleNavigableAndOperable` |
| Home only | `Easee.CrestronDriver.AndroidTests.PlacementUiTests.HomePageTileIsVisibleNavigableAndOperable` |
| Both | `Easee.CrestronDriver.AndroidTests.PlacementUiTests.BothTilesAreVisibleNavigableAndOperable` |
| Picker inspection | `Easee.CrestronDriver.AndroidTests.ReadOnlyUiTests.RoomTileNavigatesAndAccountPickersAreAccessible` |

Set `EASEE_DRIVER_UI_SETTINGS` to the existing protected fixture settings file. Picker inspection needs only `Host`, `UserName`, `Password`, and `CertificateSha256`. Placement tests additionally need `SiteId`, `ChargerId`, a fresh independent `AccessToken` and `ExpiresAt`, plus `EaseeUserName` and `EaseePassword` for Configure. These values remain private. The fixtures validate that the private selection, processor and workflow target agree.

```powershell
$env:EASEE_DRIVER_UI_SETTINGS = 'C:\Private\ui-settings.json'
./validation/Workflow/Invoke-Validation.ps1 -Mode Installed -PlanPath C:\Private\picker-plan.json -SettingsPath C:\Private\processor-settings.json -ResultsDirectory C:\Private\Runs\unique-picker-run
```

Placement scenarios issue start/stop requests and require **`-AllowStartStop`**. The launcher sets the fixture's separate opt-in only for that child execution. Run each of Room, Home and Both separately. Use a bounded per-scenario timeout of 2700 seconds and at least 3600 seconds for the enclosing transport. Placement fixtures allow 32 minutes of active work and a separate five-minute cleanup budget; leave time for SDK preparation and verification. A short transport timeout can interrupt cleanup. A full vehicle can remain noncharging after request acceptance. No current-limit or cable-lock writes are performed by these scenarios.

## Validate a plan without operating hardware

Add `-ValidateOnly` to either command. Processor settings are not read and no SDK workflow is entered; the public plan validation and project-specific gates still run. Optional live/update/start-stop switches are required when validating a plan that would need them during execution. Validation still builds on the shared HPNEIL worker under its machine reservation.

## Evidence and failures

Each run requires a fresh private results directory. It contains build/execution logs, `Coordinator.json`, `MachineLease.json`, and the SDK's `workflow/` receipts. A successful native run must have a released processor lease. Installed runs must confirm reservations released; a pass additionally requires tests, candidate verification, restoration and cleanup. Failed and uncertain outcomes remain failures. A crash, malformed/missing completion receipt or uncertain reservation retains the machine marker for exact-owner reconciliation. No workflow or gesture is automatically replayed.

The runner validates plans and uses the same SDK entry points as the completed development runs. Its own build and offline validation checks are tooling checks; they are not additional physical driver results. The candidate's retained results remain documented in the root `VALIDATION.md`.
