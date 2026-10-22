# Development and validation history

<!-- development-history -->

## 22 October 2026 — release alignment and connection-event correction

The original public 1.0.0 workflow omitted the model's NuGet distribution wrapper/publication and parts of its release/documentation workflow. The model comparison records the corrections. A local 1.0.1 fix preserved the `vehicleConnected` property while renaming its colliding event to `vehicleConnectedEvent`; 62 desktop tests passed, the corrected 70-test native suite passed, and the installed driver produced no new definition errors while the car continued charging. The first native attempt was blocked during packaging by the old 69-test declaration; no processor tests ran in that failed attempt and its lease was released.

Version 1.0.2 prepares the audited public distribution and can upgrade the installed 1.0.1 development fix. Driver and test-host licensing, the NuGet v1 manifest and wrapper, installer documentation, the NUnit policy, processor-only releases and immutable release promotion are covered in MODEL-COMPARISON.md.

The following section preserves the earlier development record. Candidate hashes and counts refer to those historical candidates, not a claim that later builds have the same bytes.

## Validation status

The release acceptance and requirement-by-requirement history are in [VALIDATION.md](VALIDATION.md). Version 1.0.1 corrects the connection event identifier; the property remains `vehicleConnected`, while the event is `vehicleConnectedEvent`. The user has confirmed actual vehicle charging initiated from the panel. Version 1.0.0 followed human visual approval of the final labels on 22 October 2026, with a repeat manual UI cycle explicitly waived by the user. Required exact-source hosted and processor checks gate publication. The historical `Verify-ValidatedCandidate.ps1` tool compares the original development candidate, so later source changes intentionally fail that comparison.

On 21 October 2026, candidate **0.1.000.0006** passed the following checks on HPNEIL and the .241 processor in Office:

| Validation | Passed |
| --- | ---: |
| Desktop NUnit tests | 61 |
| Processor NUnit tests | 69 |
| Read-only processor live tests | 4 |
| Installed-driver checks | 6 |
| Actual-app UI scenarios | 4 |

The UI scenarios separately covered **Room page**, **Home page**, **Both**, and read-only site/charger picker inspection in Crestron Home 4.11.1+pr. Placement checks verified presence/absence, readable status and power, navigation, authorized start/stop requests and final noncharging state. Home navigation was checked to issue no charger command. Picker checks verified the complete expected option lists and selected states without selecting an option. Every successful run confirmed Home restoration, the unchanged candidate payload, cleanup and reservation release. The selected site and charger were preserved, and Room-only placement was restored.

The exact package SHA256 is `09d94c09572869d8919683cfe4bfc5a985b383276b498b468e88fb7a3306cb7b`. The local manifest was synchronized from the exact embedded resource in that package. These records identify the historical candidate; subsequent label and version changes are described in VALIDATION.md. The validated package and retained evidence are staged in `artifacts/validated/0.1.000.0006/`. Debug packaging advances the revision; rebuilding does not reproduce this already validated package automatically.

Earlier failed and blocked runs remain failed results. Cleanup is documented in their workflow results or separate reconciliation receipts. During build investigation, a previously blocked driver DLL later passed unchanged. A UI binary remained blocked in controlled and crossed-folder comparisons. Normalized source paths now make the UI harness builds reproducible across source directories. The final unsigned UI assembly ran with Smart App Control enforced; no signing certificate or security-policy change was used. The cause of Windows' earlier differing verdicts remains unproved.

Start/stop request acceptance with the fully charged vehicle does not demonstrate energy delivery. Live current-limit and cable-lock writes remain unexercised. See [CHANGELOG.md](CHANGELOG.md) and [RELEASE-NOTES.md](RELEASE-NOTES.md) for the public release notes. Published checksums identify the Release assets separately from the retained development candidates.

