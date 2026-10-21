# Copyright (c) 2026 Neil Colvin. MIT License with Commons Clause; see LICENSE.
[CmdletBinding()]
param([string] $ResultsDirectory = '')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (!$ResultsDirectory) { $ResultsDirectory = Join-Path $root 'TestResults/offline' }
$null = [IO.Directory]::CreateDirectory($ResultsDirectory)
$projects = @(
    @{ Name='EaseeCrestronDriver.Core.Tests'; Minimum=54 },
    @{ Name='EaseeCrestronDriver.Tests'; Minimum=7 }
)
foreach ($project in $projects) {
    $result = Join-Path $ResultsDirectory ($project.Name + '.trx')
    if (Test-Path -LiteralPath $result) { throw 'Use fresh offline result files.' }
    & dotnet test (Join-Path $root ($project.Name + '/' + $project.Name + '.csproj')) -c Release -p:BuildDriverPackage=false --filter 'TestCategory!=Processor&TestCategory!=Live&TestCategory!=LiveSetup' --results-directory $ResultsDirectory --logger ('trx;LogFileName=' + $project.Name + '.trx')
    if ($LASTEXITCODE -ne 0) { throw ('Offline tests failed: ' + $project.Name) }
    [xml]$trx = Get-Content -LiteralPath $result -Raw
    $counters = $trx.TestRun.ResultSummary.Counters
    if ([int]$counters.passed -lt $project.Minimum -or [int]$counters.failed -ne 0 -or [int]$counters.total -ne [int]$counters.passed) {
        throw ('Offline suite was incomplete, skipped or insufficient: ' + $project.Name)
    }
}
