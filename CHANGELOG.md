# Changelog

## 0.1.000.0006 — local development candidate — 2026-10-21

Initial local Easee charger driver using EaseeChargerClient 1.0.0.

- Entity V2 implementation targeting .NET Framework 4.7.2, with attributed properties, commands and events.
- Account configuration with retained masked password, checkpointed rotating tokens and automatic fresh sign-in recovery.
- Explicit site/charger pickers with account-bound persistent selection and serial-number display.
- Charging status, power, energy, connection and schedule reporting through SignalR with REST fallback.
- Configurable Room, Home or Both tile placement. Room supports direct action plus navigation; Home opens details with Charge/Stop.
- Immediate charging, stop, temporary current limit with expiry, and permanent cable-lock control.
- NUnit 5 desktop, processor and live suites, plus actual Android application validation for all tile placements and pickers.
- NuGet ManifestUtil packaging, merged dependency closure and retained third-party licences/notices.

Validated candidate: 61 desktop tests, 69 processor tests, four read-only processor live tests, six installed-driver checks and four actual-app UI scenarios passed. The fully charged vehicle did not demonstrate energy delivery. Live current-limit/cable-lock writes were not exercised.

Post-validation handoff adds the local candidate integrity checker and repeatable HPNEIL-only public-SDK workflow tooling. Eight tooling checks passed without entering a hardware workflow. These additions do not change the candidate's driver bytes or its historical test counts. See `VALIDATION.md` and `validation/Workflow/README.md`.

This candidate has no public release tag. A published driver version will be prepared separately.
