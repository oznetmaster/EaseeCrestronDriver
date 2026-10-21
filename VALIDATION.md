# EaseeCrestronDriver requirements review

Reviewed 21 October 2026 against the agreed driver scope and the retained validation for local candidate **0.1.000.0006**.

The requested implementation and authorized validation are complete. The candidate remains local and unpublished. This review adds documentation and an integrity checker; it does not change the tested driver or represent a new hardware run.

## Requirements and evidence

| Requirement | Implementation reviewed | Verification |
| --- | --- | --- |
| Entity V2 driver; shipping code targets net472 | `EntryPoint.cs`, `EaseeDriver.cs`, driver/core project files and solution | Native SDK lifecycle and processor checks. The .NET 10 Android harness is separate validation tooling. |
| Use the released client | Core project references EaseeChargerClient **1.0.0** from NuGet | Packaged dependency closure and installed payload checks. No downstream changes to the client library. |
| Attribute declarations for exposed properties, commands and events | `EaseeDriver.Properties.cs`, `.Commands.cs`, `.Events.cs`, `.CurrentSettings.cs`, `.CommandActivity.cs` | Entity contract tests and native entity checks. Dynamic selector definitions additionally publish inline option snapshots required by the observed Home bridge. |
| Account configuration and automatic recovery | Masked retained password in driver manifest; `RecoveringTokenProvider` and `ProcessorTokenStore` | Token acquisition/recovery tests; native byte-store tests and processor live authentication. Passwords/tokens are not entity properties or diagnostic log values. Masking is not an encryption claim. |
| Explicit site and charger selection | Attributed picker properties/commands, account-bound `ProcessorSelectionStore` | Native persistence tests and actual-app picker inspection. Selection survives reconfiguration/reload; changing site clears the charger. No automatic charger selection. |
| Reporting, status/power tile and appropriate icons | `ObservationState`, `ChargerReading`, property publication and UI definition | State/ordering/unknown-value tests; real SignalR and REST data; actual-app status/power visibility. Vehicle battery percentage is omitted because the published charger data does not provide it. |
| Connected and idle scheduled-start display | `SchedulePlanner`; schedule information on tile/details | Schedule/time-zone/DST unit cases and live schedule read. A future scheduled-start tile was not established on the live charger. |
| Immediate Charge, Stop and connected-car tile action | `ChargerCommands` rereads state before writes; Charge authorizes then overrides the schedule | Offline request/guard tests and authorized actual-app start/stop scenarios. The full vehicle remained noncharging; request acceptance does not establish energy delivery. |
| Push updates with occasional polling | SignalR subscription, 60-second REST fallback, five-minute schedule refresh and schedule notification wake-up | Processor live SignalR test and observation ordering tests. Initial/reconnect snapshots establish a baseline rather than emitting fictitious charging edges. |
| Room, Home or Both tile placement | `TileLayout`, v3 UI definition and Configure choice | Separate successful real-app scenarios for all three placements in Home 4.11.1+pr. Room has action plus navigation. Home navigates to details with Charge/Stop, as agreed. Home navigation was checked to issue no charger command. |
| Temporary current and cable-lock controls | Explicit current and positive expiry; charger/cable bounds; permanent-lock setting distinct from physical lock state | Offline command checks and processor execution of the suites. Live current-limit and cable-lock writes were not exercised. Installation/site settings are outside this driver. |
| Properties, commands and events for Home programming | Programmable status/availability, charging controls, current/lock commands and seven attributed events | Contract/native tests. Event baselines and known-state transitions checked; not every physical event was generated on the live charger. |
| Model conventions, XML warnings and packaging | Byte-identical Tesla model `.editorconfig`; latest C#; XML documentation generation; warnings as errors; MIT with Commons Clause/Neil Colvin metadata | Source/build review and retained successful desktop/processor builds. NuGet ManifestUtil **29.0.10**, ILRepack, assembly patching and dependency licence hashes verified in the tested package. |
| Shared hardware guardrails | HPNEIL worker, public CrestronHomeNUnit SDK pinned in `ProcessorTestSdk.lock.json`, shared processor/Android reservations | Retained workflow and restoration receipts. Final successful runs released their reservations and preserved selected site/charger; Room-only placement restored. |

## Retained validation

| Gate | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Desktop NUnit | 61 | 0 | 0 |
| Processor NUnit | 69 | 0 | 0 |
| Read-only processor live | 4 | 0 | 0 |
| Installed-driver checks | 6 | 0 | 0 |
| Actual-app scenarios: Room, Home, Both, pickers | 4 | 0 | 0 |

These totals describe the final successful runs. Earlier failed/blocked runs remain failed records; they were not relabelled as successful tests. The earlier Windows Application Control verdict differences have no proven root cause. The final unsigned harness ran with Smart App Control enforced and without a signing certificate or policy change.

Package SHA256: `09d94c09572869d8919683cfe4bfc5a985b383276b498b468e88fb7a3306cb7b`.

The staged package, exact manifest, production source inventory and evidence are under `artifacts/validated/0.1.000.0006/`. `UiValidationEvidence.zip` contains native workflow/test results, desktop results and separate UI receipts/screenshots. `ValidationSummary.json` ties them to the candidate hash. The current manifest is the exact embedded manifest from this package, rather than a later build's incremented source manifest.

## Repeat the integrity check

From the repository root in PowerShell 7:

```powershell
./scripts/Verify-ValidatedCandidate.ps1
```

The check reads local records only. It verifies all 36 inventoried production/build-input files, rejects added implicit production source files, checks package/assembly/manifest/evidence hashes and versions, and checks retained native/UI gates and cleanup outcomes. It fails on source drift or changed package/evidence bytes. It is an integrity check against retained local records, not a cryptographic attestation or a repeat of live validation.

The verifier was exercised with the unchanged candidate and five isolated negative cases: edited production source, added source, altered package, altered evidence and an unreleased-reservation flag. All six checks behaved as expected. These checks are separate from the driver test totals above.

Debug packaging advances the manifest version. Preserve the validated package; any changed driver build needs its own candidate evidence. Documentation and this verification tool were added after the driver validation and do not change its binary.

## Remaining boundaries

Physical energy delivery with a vehicle requesting charge, live current-limit/cable-lock changes, and physical generation of every event have not been demonstrated. Those limitations are explicit; no such results are inferred from HTTP acceptance or offline tests. GitHub publication and release tagging for this driver are a separate next step.

## Repository workflow handoff

The standalone `validation/Workflow` tooling calls the published CrestronHomeNUnit 3.0.0 public API and stays outside the shipping solution. Its launcher reserves HPNEIL before building, uses fresh private evidence directories, and requires explicit live/update/start-stop opt-ins. Eight tooling checks passed on HPNEIL: valid native and installed-picker plans, driver-update/live opt-in rejection, full processor-suite enforcement, shared-pool enforcement, start/stop opt-in rejection and valid placement with opt-in. All checks used validation-only mode, entered no hardware workflow and released their machine reservation. These checks are separate from the candidate's retained physical results.
