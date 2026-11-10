# EaseeCrestronDriver 1.0.3

This patch corrects the dependency and lifecycle issues described below.

- Use EaseeChargerClient 1.0.1.
- Publish charger observations in revision order so a delayed REST refresh cannot overwrite newer confirmed push feedback.
- Enforce warnings as errors in Debug builds and retain warnings in Release builds.

## Installation

Use the driver package from [GitHub](https://github.com/oznetmaster/EaseeCrestronDriver/releases/tag/v1.0.3) or the matching NuGet distribution. Import the package, then update the existing driver in Home Setup or Configure Pro. Importing alone does not update the running instance. Existing pairing and configuration formats are retained; removal and re-add is not a routine update step.

## Validation

Passed 63 workstation Debug and 63 Release test executions, plus 72 native net472/Mono processor tests. Processor validation used fake transports and synthetic credentials; no new physical-device acceptance or endurance run is claimed. Existing processor configuration and all pairing files were preserved. See [development history](DEVELOPMENT-HISTORY.md) for provenance and limits.

This GitHub/NuGet release does not replace any submitted Crestron package. Submission-specific PDFs and correspondence are excluded from public artifacts.
