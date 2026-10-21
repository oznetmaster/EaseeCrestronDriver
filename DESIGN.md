# Confirmed behavior and validation boundaries

- Entity V2 driver, attributes for all properties, commands and events; net472 only.
- Published independent EaseeChargerClient 1.0.0 dependency; no client source changes.
- Room page, Home page or Both configurable; default Room page. Home navigates to details containing Charge/Stop; Room provides direct action plus navigation. The shared v3 definition retains both for the Room behavior in Both.
- Status and power on the tile; a known next scheduled start takes precedence for a connected idle vehicle.
- Immediate charge authorizes then overrides the session schedule. Stop deauthorizes. Observed state remains authoritative.
- SignalR primitive values are deserialized into typed observation models; unsupported, malformed, duplicate and stale updates do not republish cached state.
- Once-per-minute REST checks; five-minute schedule checks or a selected-charger schedule notification.
- Nullable telemetry and explicit availability flags; no inferred battery percentage or processor-local schedule time zone.
- Temporary current and permanent cable lock exposed. Current expiry must be positive; reported electrical/cable limits are enforced.
- Account/password retained as configured for recovery; a single owned token chain is durably checkpointed in native datastore before use.
- Explicit site/charger selection has its own account-bound native byte record; it is independent of the Account wizard's saved fields. Inline available-value snapshots bridge dynamic selectors to the Home UI.
- One command at a time, fresh telemetry before each control, no automatic write retries, explicit command activity accounting.
- Start/stop live validation authorized. Other setting writes remain unexercised.
- HPNEIL only, shared machine and processor leases, public workflow and retained evidence. Office is the chosen validation room.
- MIT with Commons Clause and Neil Colvin metadata match the model.
- Public source repository and release workflow authorized; no public driver release has been published.

Candidate 0.1.000.0006 passed 61 desktop tests, 69 native tests, four read-only processor live tests and six installed-driver checks. The exact compact-display package is installed on .241 in Office; temporary validation resources and reservations were released.

Windows Application Control blocked an earlier driver load, which later passed with unchanged bytes. A UI discovery assembly remained blocked across controlled folder comparisons. Matching source, dependencies and compiler settings built with normalized paths produced identical, discoverable UI assemblies in both C:\KR and ProgramData. The UI harness uses normalized embedded paths for reproducibility. The underlying difference in Windows verdicts remains unexplained; no signing or policy changes were made.

Actual UI validation passed for Room page, Home page, Both, and read-only site/charger picker inspection against candidate 0.1.000.0006. Placement scenarios verified visibility, readable status/power, navigation, authorized charging requests and restoration. Home navigation sent no charger command; its details buttons performed Charge/Stop. Room provided a direct action and separate navigation. Picker lists matched the complete expected labels and selected state without changing either selection. Each run confirmed the unchanged package, Home restoration, cleanup and released reservations. Room-only placement and the selected site/charger were preserved. Earlier failed runs remain failed results; their workflow outcomes or separate recovery receipts document cleanup. Request acceptance with a full vehicle is not physical energy-delivery proof.
