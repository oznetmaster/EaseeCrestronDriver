# EaseeCrestronDriver local candidate 0.1.000.0006

An Easee charger integration for Crestron Home using the published EaseeChargerClient library. Configure the Easee account, then select its site and charger in the driver's Settings page.

The tile reports charger status and charging power, or the next known scheduled start when a connected car is idle. Choose Room, Home or Both. The Room tile offers direct Charge/Stop and separate navigation; the Home tile opens details, where Charge and Stop are available.

Details report charging/session energy, lifetime energy, schedule and connection information. Charge requests immediate charging, overriding the existing schedule for that session. Settings include a temporary current limit with an explicit expiry and permanent charger-side cable lock. Vehicle battery percentage is unavailable from the charger API.

The driver targets net472, uses Entity V2 attributes, and receives SignalR updates with approximately one-minute REST fallback. Account recovery retains the configured masked password and checkpoints the rotating token session in the processor datastore. Masking does not imply encryption of the processor's configuration database.

The exact package passed desktop and processor suites, read-only live tests, installed-driver checks, and actual Home app tests for Room/Home/Both and the site/charger pickers. Testing used Home 4.11.1+pr. The connected vehicle was fully charged: start/stop acceptance and final noncharging state were verified, but energy delivery was not demonstrated. Live current-limit and cable-lock writes remain unexercised.

Package SHA256: `09d94c09572869d8919683cfe4bfc5a985b383276b498b468e88fb7a3306cb7b`.

This is a local development candidate, not a public release. Keep the validated package and its evidence together; a rebuilt Debug package advances the version and requires its own validation. Copyright Neil Colvin; MIT License with Commons Clause.
