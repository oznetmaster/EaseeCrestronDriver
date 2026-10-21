# Copyright (c) 2026 Neil Colvin.
# Licensed under the MIT License with Commons Clause. See LICENSE in the repository root.
#Requires -Version 7.0
<#
.SYNOPSIS
Checks retained candidate evidence against the current production source without rebuilding or contacting hardware.
.DESCRIPTION
This is a local integrity check against retained records, not a signature or a new live test.
Documentation and validation-tool changes do not invalidate the frozen driver source inventory.
#>
[CmdletBinding()]
param(
    [string] $RepositoryRoot = (Split-Path $PSScriptRoot -Parent),
    [string] $ValidationDirectory = ''
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$RepositoryRoot = [IO.Path]::GetFullPath($RepositoryRoot)
if (!$ValidationDirectory) { $ValidationDirectory = Join-Path $RepositoryRoot 'artifacts/validated/0.1.000.0006' }
$ValidationDirectory = [IO.Path]::GetFullPath($ValidationDirectory)

function Require-True($Value, [string] $Label) {
    if ($Value -isnot [bool] -or !$Value) { throw "Validation gate failed: $Label" }
}
function Require-Hash([string] $Path, [string] $Expected) {
    if ($Expected -notmatch '^[0-9a-f]{64}$' -or (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -ine $Expected) {
        throw "SHA256 mismatch: $Path"
    }
}
function Entry-Bytes($Archive, [string] $Name) {
    $entries = @($Archive.Entries | Where-Object FullName -IEQ $Name)
    if ($entries.Count -ne 1) { throw "Missing or duplicate evidence entry: $Name" }
    $stream = $entries[0].Open()
    $memory = [IO.MemoryStream]::new()
    try { $stream.CopyTo($memory); return ,$memory.ToArray() }
    finally { $memory.Dispose(); $stream.Dispose() }
}
function Entry-Json($Archive, [string] $Name) {
    [Text.Encoding]::UTF8.GetString((Entry-Bytes $Archive $Name)).TrimStart([char]0xFEFF) | ConvertFrom-Json
}
function Require-Tests($Tests, [int] $Expected, [string] $Label) {
    Require-True $Tests.Complete "$Label complete"
    Require-True $Tests.MeetsGate "$Label meets gate"
    if ($Expected -le 0 -or $Tests.Passed -ne $Expected -or $Tests.Failed -ne 0 -or $Tests.Skipped -ne 0) {
        throw "Unexpected test totals: $Label"
    }
}
$summary = Get-Content -LiteralPath (Join-Path $ValidationDirectory 'ValidationSummary.json') -Raw | ConvertFrom-Json
$inventory = @(Get-Content -LiteralPath (Join-Path $ValidationDirectory 'ValidatedLocalProductionSourceInventory.json') -Raw | ConvertFrom-Json)
if (!$inventory.Count) { throw 'Production source inventory is empty.' }
$paths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$prefix = $RepositoryRoot.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
foreach ($item in $inventory) {
    $path = [IO.Path]::GetFullPath((Join-Path $RepositoryRoot $item.Path))
    if (!$path.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase) -or !$paths.Add($path)) { throw 'Invalid or duplicate source inventory path.' }
    Require-Hash $path $item.Sha256
}
# SDK-style projects include added source files implicitly. Matching only the old list is insufficient.
foreach ($folder in @('EaseeCrestronDriver', 'EaseeCrestronDriver.Core')) {
    foreach ($file in Get-ChildItem -LiteralPath (Join-Path $RepositoryRoot $folder) -File -Recurse) {
        $relative = [IO.Path]::GetRelativePath($RepositoryRoot, $file.FullName)
        if ($relative -imatch '(^|[\\/])(bin|obj)[\\/]') { continue }
        if ($file.Extension -in @('.cs', '.csproj', '.props', '.targets', '.resx') -or $relative -imatch '[\\/]IncludeInPkg[\\/]') {
            if (!$paths.Contains($file.FullName)) { throw "Production file absent from tested inventory: $relative" }
        }
    }
}
Require-Hash (Join-Path $ValidationDirectory 'EaseeCrestronDriver.pkg') $summary.PackageSha256
Require-Hash (Join-Path $ValidationDirectory 'UiValidationEvidence.zip') $summary.UiEvidenceSha256
Require-Hash (Join-Path $ValidationDirectory 'EaseeCrestronDriver.json') $summary.EmbeddedManifestSha256
$manifest = Get-Content -LiteralPath (Join-Path $ValidationDirectory 'EaseeCrestronDriver.json') -Raw | ConvertFrom-Json
if ($manifest.GeneralInformation.DriverVersion -cne $summary.Version) { throw 'Manifest version differs from validation summary.' }
$package = [IO.Compression.ZipFile]::OpenRead((Join-Path $ValidationDirectory 'EaseeCrestronDriver.pkg'))
try {
    if ((Entry-Json $package 'EaseeCrestronDriver.dat').driverVersion -cne $summary.Version) { throw 'Package version differs from validation summary.' }
    $sha = [Security.Cryptography.SHA256]::Create()
    try { $hash = [BitConverter]::ToString($sha.ComputeHash((Entry-Bytes $package 'EaseeCrestronDriver.dll'))).Replace('-', '') }
    finally { $sha.Dispose() }
    if ($hash -ine $summary.PackagedDriverAssemblySha256) { throw 'Packaged driver assembly differs from validation summary.' }
} finally { $package.Dispose() }
Require-True $summary.AllReservationsReleased 'all reservations released'
Require-True $summary.SelectedSiteAndChargerPreserved 'site/charger preserved'
Require-True $summary.ProductionImplementationMatchesTestedSource 'tested source provenance'
if ($summary.PlacementTestsFinalCharging -ne $false) { throw 'Placement tests did not finish noncharging.' }
$evidence = [IO.Compression.ZipFile]::OpenRead((Join-Path $ValidationDirectory 'UiValidationEvidence.zip'))
try {
    $workflow = Entry-Json $evidence 'Native/Workflow.json'
    Require-True $workflow.Passed 'native workflow'
    Require-True $workflow.DriverUpdateVerified 'installed candidate verified'
    $totals = [ordered]@{
        'Local' = $summary.DesktopTestsPassed
        'Processor' = $summary.ProcessorTestsPassed
        'Processor live' = $summary.ProcessorReadOnlyLiveTestsPassed
        'Deployed driver live' = $summary.InstalledDriverChecksPassed
    }
    foreach ($label in $totals.Keys) {
        $stage = @($workflow.Stages | Where-Object Stage -IEQ $label)
        if ($stage.Count -ne 1) { throw "Missing or duplicate workflow stage: $label" }
        Require-Tests $stage[0].Tests $totals[$label] $label
    }
    $cases = @(Entry-Json $evidence 'UiValidationSummary.json')
    if ($cases.Count -ne 4 -or @($summary.ActualUiCases).Count -ne 4) { throw 'Expected four actual-app scenarios.' }
    foreach ($label in @('Room', 'Home', 'Both', 'Pickers')) {
        $case = @($cases | Where-Object Case -IEQ $label)
        if ($case.Count -ne 1) { throw "Missing or duplicate UI scenario: $label" }
        $case = $case[0]
        $outcome = Entry-Json $evidence "$label/Outcome.json"
        foreach ($gate in @('Passed', 'RestorationConfirmed', 'CleanupConfirmed', 'CandidateVerified', 'ReservationsReleased')) {
            Require-True $case.Outcome.$gate "$label summary $gate"
            Require-True $outcome.$gate "$label receipt $gate"
        }
        Require-Tests $outcome.Tests 1 $label
        Require-True $case.PrivateInputsRemoved "$label private inputs removed"
        if ($case.PackageSha256 -ine $summary.PackageSha256 -or $case.DriverVersion -cne $summary.Version) { throw "UI candidate mismatch: $label" }
    }
    $pickers = Entry-Json $evidence 'Pickers/PickerInspectionObserved.json'
    foreach ($gate in @('SelectionPreserved', 'CommandActivityUnchanged', 'Online')) { Require-True $pickers.$gate "pickers $gate" }
    if ($pickers.Charging -ne $false) { throw 'Picker inspection did not finish noncharging.' }
} finally { $evidence.Dispose() }
[pscustomobject]@{
    Version = $summary.Version
    PackageSha256 = $summary.PackageSha256
    ProductionFilesVerified = $inventory.Count
    DesktopTestsPassed = $summary.DesktopTestsPassed
    ProcessorTestsPassed = $summary.ProcessorTestsPassed
    ProcessorReadOnlyLiveTestsPassed = $summary.ProcessorReadOnlyLiveTestsPassed
    InstalledDriverChecksPassed = $summary.InstalledDriverChecksPassed
    ActualUiCasesPassed = 4
    RetainedEvidenceVerified = $true
    HardwareContacted = $false
    CheckedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
}
