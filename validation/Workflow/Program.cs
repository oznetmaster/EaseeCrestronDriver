using System.Net;
using System.Text.Json;

using CrestronHomeNUnit.Workflow;

namespace Easee.CrestronDriver.Workflow;

// Desktop workflow tooling only. The driver solution and every shipping assembly remain net472.
internal static class Program
	{
	private static readonly JsonSerializerOptions Json = new () { PropertyNameCaseInsensitive = true, WriteIndented = true };
	private sealed record Settings (string UserName, string Password);
	private sealed record Receipt (string Mode, bool ValidationOnly, bool WorkflowEntered, bool Passed, bool ReservationsReleased, string? FailureType = null);
	private static readonly string[] PlacementTests =
		[
		"Easee.CrestronDriver.AndroidTests.PlacementUiTests.RoomPageTileIsVisibleNavigableAndOperable",
		"Easee.CrestronDriver.AndroidTests.PlacementUiTests.HomePageTileIsVisibleNavigableAndOperable",
		"Easee.CrestronDriver.AndroidTests.PlacementUiTests.BothTilesAreVisibleNavigableAndOperable"
		];
	private const string PickerTest = "Easee.CrestronDriver.AndroidTests.ReadOnlyUiTests.RoomTileNavigatesAndAccountPickersAreAccessible";

	private static T Read<T> (string path) => JsonSerializer.Deserialize<T> (File.ReadAllText (path), Json)
		 ?? throw new InvalidDataException ("Required private input is empty.");
	private static void Require (bool condition, string message)
		{
		if (!condition)
			throw new InvalidDataException (message);
		}
	private static void RequireWorker ()
		{
		Require (string.Equals (Environment.MachineName, "HPNEIL", StringComparison.OrdinalIgnoreCase), "Use the HPNEIL worker.");
		string? owner = Environment.GetEnvironmentVariable ("EASEE_DRIVER_MACHINE_OWNER");
		Require (Guid.TryParseExact (owner, "N", out _), "Launch through Invoke-Validation.ps1 with a machine reservation.");
		Require (File.ReadAllText (@"C:\ProgramData\CrestronSubmissionEvidence\machine.lock") == owner, "Machine reservation ownership differs.");
		}
	private static void RequireHost (string host) => Require (host is "192.168.8.241" or "192.168.8.244", "Select an explicit processor from the shared pool.");
	private static void CheckNative (WorkflowPlan plan, string[] args)
		{
		plan.Validate ();
		RequireHost (plan.Host);
		Require (!plan.AllowProcessorReboot, "This driver validation does not authorize processor reboots.");
		Require (plan.AndroidTests is null && plan.DeployedControls.Length == 0, "Run UI/control validation through a separate pinned installed-driver plan.");
		Require (plan.ActualDriver is null || args.Contains ("--allow-driver-update"), "Driver update requires explicit enablement.");
		Require (plan.LiveSuites.Length == 0 || args.Contains ("--allow-live"), "Live tests require explicit enablement.");
		Require (plan.LiveSuites.All (s => s.Id == "easee-driver-live" && s.MinimumPassed >= 4), "Only the required read-only live suite belongs in this workflow.");
		Require (plan.ProcessorSuites.Length == 1 && plan.ProcessorSuites[0].Id == "easee-driver" && plan.ProcessorSuites[0].MinimumPassed >= 69,
			"Require the complete processor suite.");
		Require (plan.RemoveTestInstanceAfterRun && plan.RemoveTestPackageAfterSuccessfulRun, "Require temporary test-instance and package cleanup.");
		Require (plan.LocalTests.Any (t => Path.GetFileName (t.Project) == "EaseeCrestronDriver.Core.Tests.csproj" && t.MinimumPassed >= 54) &&
			plan.LocalTests.Any (t => Path.GetFileName (t.Project) == "EaseeCrestronDriver.Tests.csproj" && t.MinimumPassed >= 7), "Require both desktop suites.");
		Require (Path.GetFileName (plan.TestPackage.Project) == "EaseeCrestronDriver.ProcessorTests.csproj", "Wrong processor package project.");
		Require (plan.ActualDriver is null || Path.GetFileName (plan.ActualDriver.Project) == "EaseeCrestronDriver.csproj", "Wrong driver project.");
		}
	private static void CheckInstalled (InstalledDriverTestPlan plan, string[] args)
		{
		plan.Validate ();
		RequireHost (plan.Host);
		Require (!plan.AttendedPhysicalObservation && plan.OperatorReadiness is null, "Use the bounded automated installed-driver workflow.");
		Require (plan.Target.Model == "Easee Charger" && plan.Target.Developer == "Neil Colvin", "Wrong installed driver identity.");
		Require (plan.AndroidTests.RequiredTests?.Count == 1, "Run each UI scenario separately with its own retained outcome.");
		string test = plan.AndroidTests.RequiredTests![0];
		Require (test == PickerTest || PlacementTests.Contains (test), "Select an exact Easee UI scenario.");
		Require (plan.AndroidTests.ManagedChildren.Count == 0, "This driver has no managed children.");
		Require (test == PickerTest || args.Contains ("--allow-start-stop"), "Placement scenarios require start/stop enablement.");
		Require (test == PickerTest || Environment.GetEnvironmentVariable ("EASEE_DRIVER_UI_ALLOW_START_STOP") == "1", "Missing fixture start/stop enablement.");
		}
	public static async Task<int> Main (string[] args)
		{
		if (args.Length < 4 || args[0] is not ("native" or "installed"))
			{
			Console.Error.WriteLine ("Usage: native|installed PLAN SETTINGS RESULTS [--validate-only] [--allow-live] [--allow-driver-update] [--allow-start-stop]");
			return 2;
			}
		string mode = args[0], planPath = args[1], settingsPath = args[2], results = args[3];
		bool validateOnly = args.Contains ("--validate-only");
		var receipt = new Receipt (mode, validateOnly, false, false, true);
		bool ownsReceipt = false;
		string receiptPath = Path.Combine (results, "Coordinator.json");
		void Save () => File.WriteAllText (receiptPath, JsonSerializer.Serialize (receipt, Json));
		using var cancellation = new CancellationTokenSource ();
		ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; cancellation.Cancel (); };
		Console.CancelKeyPress += cancel;
		try
			{
			RequireWorker ();
			Require (Directory.Exists (results) && !File.Exists (receiptPath), "Use a fresh launcher evidence directory.");
			Save ();
			ownsReceipt = true;
			var native = mode == "native" ? Read<WorkflowPlan> (planPath) : null;
			var installed = mode == "installed" ? Read<InstalledDriverTestPlan> (planPath) : null;
			if (native is not null)
				CheckNative (native, args);
			else
				CheckInstalled (installed!, args);
			if (validateOnly)
				{
				receipt = receipt with { Passed = true };
				Save ();
				return 0;
				}
			var settings = Read<Settings> (settingsPath);
			Require (!string.IsNullOrWhiteSpace (settings.UserName) && !string.IsNullOrEmpty (settings.Password), "Private processor credentials are required.");
			var credential = new NetworkCredential (settings.UserName, settings.Password);
			RequireWorker ();
			receipt = receipt with { WorkflowEntered = true, ReservationsReleased = false };
			Save ();
			string phase = Path.Combine (results, "workflow");
			if (native is not null)
				{
				var outcome = await WorkflowRunner.RunAsync (native, credential, phase, _ => { }, cancellation.Token).ConfigureAwait (false);
				bool released = File.Exists (Path.Combine (phase, "Lease.json")) &&
					Read<JsonElement> (Path.Combine (phase, "Lease.json")).GetProperty ("State").GetString () == "Released";
				receipt = receipt with { Passed = outcome.Passed && released, ReservationsReleased = released };
				}
			else
				{
				var outcome = await InstalledDriverTests.RunAsync (installed!, credential, phase, cancellation.Token).ConfigureAwait (false);
				receipt = receipt with { Passed = outcome.Passed, ReservationsReleased = outcome.ReservationsReleased };
				}
			Save ();
			return receipt.Passed ? 0 : 1;
			}
		catch (Exception error)
			{
			receipt = receipt with { Passed = false, FailureType = error.GetType ().Name };
			// Exception messages may include private transport data. Retain the type and SDK receipts only.
			if (ownsReceipt)
				Save ();
			Console.Error.WriteLine ("Workflow failed (" + error.GetType ().Name + "). Inspect private coordinator/SDK receipts before retrying.");
			return 1;
			}
		finally { Console.CancelKeyPress -= cancel; }
		}
	}
