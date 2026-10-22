# EaseeCrestronDriver 1.0.0

First stable public release of the Easee charger driver for Crestron Home, using the published EaseeChargerClient 1.0.0 library.

- Entity V2 driver targeting .NET Framework 4.7.2, with attributed properties, commands and events.
- Account configuration, automatic sign-in recovery, rotating token persistence, and explicit site/charger pickers with serial numbers.
- Status, charging power, session/lifetime energy, connection and schedule reporting through SignalR with one-minute REST fallback.
- Room, Home or Both tile placement. Room provides direct Charge/Stop plus navigation; Home opens the detail page with Charge/Stop.
- Immediate charging requests, stop, temporary current-limit overrides with an explicit duration, and permanent cable-lock control.
- Clear labels distinguish charging authorization, the reported charging current limit and a temporary override.

Validation includes 61 desktop tests, 69 processor tests, four read-only processor live tests, six installed-driver checks and four actual-app scenarios covering Room/Home/Both and site/charger pickers. These retained hardware/UI results identify their respective development and Release candidates in VALIDATION.md; they are not claims that every rebuilt package was physically retested. On 22 October 2026 the user visually approved the label update installed on the review processor and authorized publication without repeating its manual UI suites. Required exact-source CI checks still gate this release.

Testing used Crestron Home 4.11.1+pr. The connected vehicle was fully charged: start/stop acceptance and final noncharging state were verified, but energy delivery was not demonstrated. Live current-limit and cable-lock writes remain unexercised. The charger API does not provide vehicle battery percentage. Masked account configuration is not an encryption claim.

Install EaseeCrestronDriver.pkg; the separate ProcessorTests package is validation tooling. SourceIdentity.json and SHA256SUMS.txt identify the published build. Documentation and original dependency licences/notices are included in the documentation ZIP.

Copyright Neil Colvin. MIT License with Commons Clause.
