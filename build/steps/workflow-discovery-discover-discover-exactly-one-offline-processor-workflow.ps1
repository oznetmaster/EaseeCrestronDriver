$output = @(dotnet test validation/WorkflowTests/EaseeCrestronDriver.WorkflowTests.csproj -c Release --list-tests 2>&1)
$output | Write-Output
if ($LASTEXITCODE -ne 0) { throw 'Workflow discovery failed.' }
if (@($output | Where-Object { $_.ToString().Trim() -ieq 'EaseeCrestronDriver processor workflow' }).Count -ne 1) { throw 'Expected one named workflow.' }
