# Repository and release workflow

Public source: [oznetmaster/EaseeCrestronDriver](https://github.com/oznetmaster/EaseeCrestronDriver), branch `master`. The independent EaseeChargerClient has its own repository and release. The model comparison and intentional differences are recorded in [MODEL-COMPARISON.md](MODEL-COMPARISON.md).

## Required checks

`tests.yml` runs the offline net472 NUnit 5 suites, release-policy checks and desktop tooling builds. `workflow-discovery.yml` checks public-SDK workflow discovery. `nunit5-policy.yml` uses the same pinned reusable policy as the model. Public workflows receive no Easee or processor credentials.

The private CrestronHomeHardwareCI bridge runs target `easeecrestrondriver_driver` on HPNEIL, using the pinned public SDK and a reserved processor from the shared pool. It executes the 70-test processor suite and removes its temporary instance/archive. The App reports `Processor tests / easeecrestrondriver_driver` for the exact source commit. `RELEASE_REQUIRED_CHECKS` binds this context to App 4950827. There is no hardware-skip publication input. Retained or uncertain reservations require exact-owner reconciliation.

## Prepare once, validate, then publish

1. Prepare the three-part version in both driver manifests and the release-notes heading, then commit it and obtain all required exact-source checks.
2. Dispatch **Release Package** on that source, supply `version`, and leave `publish=false`. It runs the gates and offline tests, builds both Release packages, packs the NuGet wrapper, tests package-content guards, and stages documentation, source identity and checksums. Retain its run ID and download `release-assets`.
3. Validate the exact staged driver on the reserved processor, retaining its hash and limits. Do not transfer another candidate's physical validation to a rebuild. The initial 1.0.0 label-only retest waiver in VALIDATION.md is historical and is not a general waiver for subsequent releases.
4. Create the matching `vVERSION` tag on the exact source commit. Tags alone do not publish this workflow. Dispatch **Release Package on that tag**, supply the same version, set `publish=true` and set `staged_run_id` to the successful preparation run.
5. Promotion verifies the repository, workflow path, preparation job, successful conclusion, exact source/tag/version, artifact checksums and NuGet payload, then rechecks all source gates. It publishes the already staged NuGet package through OIDC, followed by the same GitHub assets. It does not rebuild them. A repeated NuGet publication is accepted only if the existing package contains the same verified driver payload.

The NuGet job uses environment `nuget`, `id-token: write`, `NuGet/login@v1` and repository variable `NUGET_USER`. Configure a NuGet trusted publisher for owner/repository `oznetmaster/EaseeCrestronDriver`, workflow `release-package.yml`, environment `nuget`, restricted to `CrestronHomeDriver.Easee.Charger` and pushing new packages/versions. GitHub release creation uses the existing `github-release` environment. No API-key secret is required.

## Distribution contract

`CrestronHomeDriver.Easee.Charger` conforms to the community [Crestron Home Driver NuGet Publishing Standard v1](https://github.com/oznetmaster/Crestron-Home-Driver-Feed-Installer/blob/master/docs/standard/crestron-home-driver-nuget-publishing-standard-v1.md). It is a delivery wrapper for a final Release `.pkg`, not a .NET DLL reference package. This standard is not an official Crestron specification.

`Assert-DriverNuGetPackage.ps1` verifies metadata, required tags, manifest fields/version, README/licence, exactly one root payload, no library DLL/dependencies and matching driver SHA256. `Test-DriverNuGetPackage.ps1` exercises the actual generated package and four corrupt-package cases. `Assert-StagedRelease.ps1` checks the complete release asset set, provenance and hashes.

GitHub assets include the main and processor `.pkg` files, NuGet package, distribution manifest, documentation ZIP, source identity and SHA256SUMS. The documentation includes original packaged notices. Post-publication validation independently downloads GitHub/NuGet assets and checks payload identity. NuGet upload acceptance precedes public validation and indexing; the check allows 65 minutes for that documented processing window and fails unexpected HTTP responses immediately. A delayed check can be dispatched again with the published version, without rebuilding or republishing the release. No raw hardware logs, credentials, account profiles or resolved private plans are published.

The immutable 1.0.2 NuGet README contains repository-relative documentation links that NuGet does not resolve. Its installation, GitHub Release and project links work; use the repository README for the linked technical documentation. The default-branch README now uses absolute GitHub links for subsequent packages. This documentation correction does not change the published driver or package.

## Processor-only releases

Use **Release processor tests** from reviewed `master` for test-only changes. Its independent three-part version controls the test host, with the published SDK pinned and all source gates required. A dry run stages the test package, documentation, prepared test manifest, source identity and checksums. With `publish=true`, it creates `processor-tests-vVERSION` on the reviewed source and a GitHub release with `latest=false`. It does not publish or update the driver or either NuGet package. Runtime changes require a product release instead.

The shipping driver/core remain net472; desktop workflow/discovery/Android tools remain separate .NET 10 projects. No code-signing certificate, Windows security-policy change, processor reboot or charger command is part of publication.
