# Easee Driver Tests processor package

Open the repository solution and build `EaseeCrestronDriver.ProcessorTests.csproj` in Visual Studio. Command-line builds skip processor packaging unless `-p:BuildProcessorTestPackages=true` is supplied, so ordinary CI builds do not require the processor SDK. The package is written to `bin\Debug\net472\EaseeCrestronDriver.ProcessorTests.pkg`.

The project imports the shared host and build tools from `ProcessorTestSdkRoot`. Override that property in `EaseeCrestronDriver.ProcessorTests.Local.targets` if the SDK checkout is elsewhere. Source tests remain in the referenced test project; no fixture copies are maintained here. NUnit and all application dependencies are merged; Crestron SDK libraries remain platform dependencies.

The build stages the Home UI, validates discovery from the merged and patched assembly, constructs the package, and normalizes its ZIP entries. Debug builds increment the build version; Release version increments are reserved for CI. Auto-deployment uses the existing shared SFTP script and the local `.csproj.user` settings, only for Debug builds inside Visual Studio.

Driver name: **Easee Driver Tests**. Manufacturer: **Neil Colvin**. Category: **Utility**. TCP port: **automatic by default** (`Port=0`). Find the package using **Find packages** in the runner; the Home tile shows its current port for manual connections.

The Windows runner discovers the suites declared in ProcessorTests.json. Use -IncludeLiveTests when generating a package with a separate Live category. Unit Tests are selected by default. Live Tests require explicit selection and may operate physical devices.

Choose configuration files using **Test inputs…** in the Windows runner before discovery or execution. Inputs are kept outside the package and sent over an authenticated, encrypted configuration channel. Each suite has its own inputs. Tests locate files through `TestContext.Parameters.Get("TestDataDirectory", AppContext.BaseDirectory)`. The test suite must reload its configuration between operations rather than cache the first discovery indefinitely.

Supply the files required by the live suite using **Test inputs…**. Fixture configuration must honor `TestContext.Parameters.Get ("EnableLiveTests", configuredDefault)` so explicit selection overrides any legacy JSON enable switch. The runner supplies the NUnit `EnableLiveTests` parameter for the selected live suite, overriding the JSON switch for this operation only. The JSON `Enabled` value can remain false. Secrets are never automatically embedded in the `.pkg`. Transferred files reside in the driver's private data directory and remain stored until replaced or cleared through the runner. Live runs cannot be started from the Home tile.

Build the latest Windows runner before connecting. After deployment and activation, `PrepareRunner.ps1` imports existing deployment credentials into Windows-protected local storage. Use **Find packages** and select a package. It connects automatically when processor credentials are available; otherwise enter the SFTP username and password and use **Connect**. The Home tile displays the current TCP port; there is no pairing key to enter.

The package validator discovers every suite, but its optional `--run-twice` check executes only suites whose `ManualOnly` property is false. Do not remove that property from hardware or other opt-in suites.