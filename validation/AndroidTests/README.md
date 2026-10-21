# Installed Home app validation

This standalone NUnit 5 harness runs on the Windows test computer using the published CrestronHomeNUnit Android API, which requires .NET 10. It is outside the shipping driver solution; every driver binary remains net472.

Run only through the public installed-driver workflow with processor and shared Android reservations, pinned candidate bytes, a private Android profile and explicit private processor settings. Normal discovery does not connect or operate devices. Captures and settings stay in private evidence storage.

Validation must exercise Room page, Home page and Both in the actual app, including complete visibility checks, navigation and explicitly authorized charging actions. Schema-only tests do not establish UI support. Setup/cleanup and each placement require separate retained results. Unknown input outcomes are never replayed.

Home acceptance uses tile navigation, confirms that navigation sent no charger command, then exercises the details Charge and Stop buttons. Room acceptance uses the tile action for immediate charging, then the separate navigation control to inspect details and stop. Both exercises both paths. The app version observed during development is 4.11.1+pr.

A discovery failure is not an executed test. Retain the original error, inspect whether the fixture started, confirm the unchanged candidate and restored UI, and reconcile only the exact owned leases before retrying. Do not rebuild or relocate assemblies merely to change an Application Control verdict.

Display-text searches use ordinal case-insensitive matching and reject ambiguous labels, then supply the observed literal to the public SDK locator. Resource identifiers, account keys, credentials, tokens and selected site/charger IDs retain their exact semantics. Picker inspection reads option labels and selected state without selecting an option, then uses the picker toolbar Back control to dismiss the list.

Read-only picker validation needs only the protected processor settings. It takes each management snapshot through a fresh authenticated connection, checks selection and command activity before/after, and confirms the charger remains online and noncharging. It does not create an unused Easee cloud-observer session. Start/stop fixtures retain their independent expiring Easee observer. Non-scrollable picker lists are checked in their entirety without sending a swipe to selectable rows.
