# Repository and release workflow

Public source: [oznetmaster/EaseeCrestronDriver](https://github.com/oznetmaster/EaseeCrestronDriver), default branch `master`. The driver uses the independent published EaseeChargerClient library. No driver changes or release material belong in that library's repository.

## Required source checks

`tests.yml` runs the two offline net472 NUnit 5 suites, release-policy checks, and builds the separate desktop validation tools. `workflow-discovery.yml` proves offline discovery of the published-SDK processor workflow. Neither public workflow operates hardware or receives account credentials.

Processor testing is performed by the existing **private CrestronHomeHardwareCI bridge on HPNEIL**, under the target `easeecrestrondriver_driver`, using the published SDK revision in `ProcessorTestSdk.lock.json`. The protected templates select an available processor from the shared .241/.244 pool and Office room. This is a test-only CI plan: it runs the complete 69-test processor suite and removes its temporary instance/archive. It does not update the actual driver or operate the charger.

The existing GitHub App reports `Processor tests / easeecrestrondriver_driver` against the exact reviewed source commit. `RELEASE_REQUIRED_CHECKS` binds that context to App **4950827**. The release helper also checks the latest successful `tests.yml` and `workflow-discovery.yml` runs for the same source. Missing configuration, missing/pending checks, another App/revision or a nonpassing result block release. There is no hardware-skip input in this release workflow. These are release gates; no new branch protection or runner labels are assumed.

Use the private bridge's deliberate target dispatch after hosted checks pass. Its existing global scheduling policy is preserved. HPNEIL and the processors are shared resources; the Easee target takes the machine reservation and the public SDK takes the processor lease. An uncertain run retains ownership for reconciliation. No existing reservation is stolen, and SCOTTISHNEIL is not a validation runner for this driver.

## Release dry run

Run **Release Package** manually on `master`, supply a three-part version matching the manifest's first three components (optionally suffixed for prerelease), and leave **publish** false. This verifies required checks, reruns offline tests, builds both Release `.pkg` files with NuGet ManifestUtil 29.0.10 and the locked public processor SDK, validates the actual package versions, and stages driver/test packages, documentation, source identity and checksums as a GitHub Actions artifact. It does not create a tag or public release.

The source App check certifies the tested source/test package. It does not certify independently rebuilt Release driver bytes. Before publishing a new driver candidate, validate those exact bytes through the installed-driver workflow and required actual UI scenarios, retaining the package hash and limitations. The original development candidate's evidence cannot be transferred to a new version or rebuilt package.

For the initial 1.0.0 release, the user visually approved the final label update and explicitly waived repeating its manual UI suites before authorizing publication. VALIDATION.md records the approved package and the distinction between historical exact-byte evidence and the new versioned Release build. This specific approval does not waive the required exact-source hosted or processor gates.

## Publish an explicitly prepared release

Prepare the intended three-part version in the source manifest and root release notes, then commit it and obtain passing exact-source checks. The release-notes heading must be `# EaseeCrestronDriver VERSION`. A release-build package uses the corresponding four-part driver version ending in `.0`; Debug packaging advances its build component. The workflow rejects a tag/manifest mismatch.

Create the exact `vVERSION` tag on the reviewed commit. A tag push invokes publication, or manually run the workflow **on that tag** with **publish** true. Publication rejects a branch checkout or a tag identifying another commit. The publish job uses the `github-release` environment and only uploads assets produced by the successful preparation job; it never rebuilds them. Workflow setup does not create or approve the first public driver release.

GitHub distribution includes the driver `.pkg`, separate processor test `.pkg`, a documentation ZIP, source identity and SHA256 checksums. No NuGet publication credentials are needed for these GitHub assets. Account files, Android profiles, resolved private plans, raw hardware logs and lease evidence remain off the public repository/artifacts. The documentation ZIP includes original dependency licences/notices from the actual packaged driver.

The .NET 10 workflow/discovery/Android projects are desktop tooling outside the shipping solution. The driver and core remain net472. No code-signing certificate or Windows security-policy change is part of this workflow.
