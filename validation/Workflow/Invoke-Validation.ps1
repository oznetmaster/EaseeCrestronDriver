# Copyright (c) 2026 Neil Colvin. MIT License with Commons Clause; see LICENSE.
#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Native','Installed')][string] $Mode,
    [Parameter(Mandatory)][string] $PlanPath,
    [string] $SettingsPath = '',
    [Parameter(Mandatory)][string] $ResultsDirectory,
    [switch] $ValidateOnly,
    [switch] $AllowLive,
    [switch] $AllowDriverUpdate,
    [switch] $AllowStartStop
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($env:COMPUTERNAME -ine 'HPNEIL') { throw 'Use the HPNEIL worker.' }
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')).TrimEnd('\', '/')
function Private-Path([string] $Path) {
    if (![IO.Path]::IsPathFullyQualified($Path)) { throw 'Use absolute private input/evidence paths.' }
    $full = [IO.Path]::GetFullPath($Path)
    if ($full -ieq $repository -or $full.StartsWith($repository + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Private inputs and results must remain outside the checkout.'
    }
    return $full
}
$PlanPath = Private-Path $PlanPath
$ResultsDirectory = Private-Path $ResultsDirectory
if (!(Test-Path -LiteralPath $PlanPath -PathType Leaf)) { throw 'The private plan is missing.' }
if (!$ValidateOnly) {
    $SettingsPath = Private-Path $SettingsPath
    if (!(Test-Path -LiteralPath $SettingsPath -PathType Leaf)) { throw 'Private processor settings are missing.' }
}
else { $SettingsPath = '-' }
if (Test-Path -LiteralPath $ResultsDirectory) { throw 'Use a fresh results directory.' }
if (Get-Process -Name Runner.Worker,dotnet,msbuild,vstest.console -ErrorAction SilentlyContinue) { throw 'HPNEIL has active work; no reservation acquired.' }
$marker = 'C:\ProgramData\CrestronSubmissionEvidence\machine.lock'
$owner = [Guid]::NewGuid().ToString('N')
$stream = [IO.File]::Open($marker, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::Read)
try { $bytes = [Text.Encoding]::UTF8.GetBytes($owner); $stream.Write($bytes); $stream.Flush($true) } finally { $stream.Dispose() }
$entered = $false
$previousOwner = $env:EASEE_DRIVER_MACHINE_OWNER
$previousControl = $env:EASEE_DRIVER_UI_ALLOW_START_STOP
try {
    if ([IO.File]::ReadAllText($marker) -cne $owner) { throw 'Machine reservation readback mismatch.' }
    $null = [IO.Directory]::CreateDirectory($ResultsDirectory)
    $acl = [Security.AccessControl.DirectorySecurity]::new()
    $acl.SetAccessRuleProtection($true, $false)
    foreach ($sid in @([Security.Principal.WindowsIdentity]::GetCurrent().User, [Security.Principal.SecurityIdentifier]::new('S-1-5-18'), [Security.Principal.SecurityIdentifier]::new('S-1-5-32-544'))) {
        $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($sid, 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow'))
    }
    Set-Acl -LiteralPath $ResultsDirectory -AclObject $acl
    [ordered]@{ Owner = $owner; State = 'Held'; Worker = $env:COMPUTERNAME } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $ResultsDirectory 'MachineLease.json')
    & dotnet build (Join-Path $PSScriptRoot 'Workflow.csproj') -c Release -p:BuildDriverPackage=false --disable-build-servers --nologo *> (Join-Path $ResultsDirectory 'Build.log')
    if ($LASTEXITCODE -ne 0) { throw 'Workflow tooling build failed; inspect the private build log.' }
    $env:EASEE_DRIVER_MACHINE_OWNER = $owner
    $env:EASEE_DRIVER_UI_ALLOW_START_STOP = if ($AllowStartStop) { '1' } else { $null }
    $arguments = @($Mode.ToLowerInvariant(), $PlanPath, $SettingsPath, $ResultsDirectory)
    if ($ValidateOnly) { $arguments += '--validate-only' }
    if ($AllowLive) { $arguments += '--allow-live' }
    if ($AllowDriverUpdate) { $arguments += '--allow-driver-update' }
    if ($AllowStartStop) { $arguments += '--allow-start-stop' }
    $entered = $true
    & dotnet (Join-Path $PSScriptRoot 'bin/Release/net10.0/EaseeCrestronDriver.Workflow.dll') @arguments *> (Join-Path $ResultsDirectory 'Execution.log')
    if ($LASTEXITCODE -ne 0) { throw 'Workflow did not pass; inspect its private receipts before retrying.' }
} finally {
    $env:EASEE_DRIVER_MACHINE_OWNER = $previousOwner
    $env:EASEE_DRIVER_UI_ALLOW_START_STOP = $previousControl
    $receiptPath = Join-Path $ResultsDirectory 'Coordinator.json'
    $release = !$entered
    if ($entered -and (Test-Path -LiteralPath $receiptPath)) {
        try { $receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json; $release = $receipt.ReservationsReleased -eq $true }
        catch { $release = $false }
    }
    if ($release -and [IO.File]::ReadAllText($marker) -ceq $owner) {
        Remove-Item -LiteralPath $marker
        if (Test-Path -LiteralPath $ResultsDirectory) {
            [ordered]@{ Owner = $owner; State = 'Released'; Worker = $env:COMPUTERNAME } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $ResultsDirectory 'MachineLease.json')
        }
    } else { Write-Warning 'Machine reservation retained. Reconcile the exact owner and processor/UI receipts before another run.' }
}
