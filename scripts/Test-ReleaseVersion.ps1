# Copyright (c) 2026 Neil Colvin. MIT License with Commons Clause; see LICENSE.
$ErrorActionPreference = 'Stop'
$temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$fixture = Join-Path $temporaryRoot ('EaseeReleaseChecks-' + [Guid]::NewGuid().ToString('N'))
$null = [IO.Directory]::CreateDirectory($fixture)
$manifest = Join-Path $fixture 'manifest.json'
$checks = 0
function Set-Manifest([string]$Version) {
    @{GeneralInformation=@{DriverVersion=$Version;VersionDate='2000-01-01'}} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $manifest
}
function Run-Bump([string]$Initial, [string]$Configuration, [bool]$Ci, [string]$Expected, [string]$Release='', [bool]$Reject=$false) {
    Set-Manifest $Initial
    $start = [Diagnostics.ProcessStartInfo]::new('pwsh')
    $start.UseShellExecute=$false; $start.RedirectStandardOutput=$true; $start.RedirectStandardError=$true
    foreach ($arg in @('-NoProfile','-NonInteractive','-File',(Join-Path $PSScriptRoot 'BumpDriverVersion.ps1'),'-ManifestPath',$manifest,'-Configuration',$Configuration)) { $start.ArgumentList.Add($arg) }
    if ($Release) { $start.ArgumentList.Add('-ReleaseVersion'); $start.ArgumentList.Add($Release) }
    foreach ($key in @('CI','TF_BUILD','GITHUB_ACTIONS')) { $start.Environment[$key]=if($Ci){'true'}else{'false'} }
    $process=[Diagnostics.Process]::Start($start)
    try {
        $stdout=$process.StandardOutput.ReadToEndAsync(); $stderr=$process.StandardError.ReadToEndAsync()
        if (!$process.WaitForExit(30000)) { $process.Kill($true); throw 'Version check timed out.' }
        $null=$stdout.GetAwaiter().GetResult(); $null=$stderr.GetAwaiter().GetResult()
        if (($process.ExitCode -ne 0) -ne $Reject) { throw 'Unexpected version-tool outcome.' }
        if (!$Reject -and (Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json).GeneralInformation.DriverVersion -cne $Expected) { throw 'Unexpected manifest version.' }
        $script:checks++
    } finally { $process.Dispose() }
}
function Reject-Action([scriptblock]$Action) {
    $failed=$false
    try { & $Action | Out-Null } catch { $failed=$true }
    if (!$failed) { throw 'Invalid release input was accepted.' }
    $script:checks++
}
try {
    Run-Bump '0.1.000.0005' Debug $false '0.1.000.0006'
    Run-Bump '0.1.000.0005' Release $false '0.1.000.0005'
    Run-Bump '0.1.000.0005' Release $true '0.1.000.0000' '0.1.0'
    Run-Bump '0.1.000.0000' Release $true '0.1.000.0000' '0.1.0'
    Run-Bump '0.1.000.0005' Release $true '' '0.2.0' $true
    Run-Bump '0.1.000.65535' Debug $false '' '' $true
    Set-Manifest '0.1.000.0000'
    $selected=& (Join-Path $PSScriptRoot 'Get-DriverReleaseVersion.ps1') -ManifestPath $manifest -Tag 'v0.1.0-preview.1' -RequireTag
    if ($selected.PackageVersion -cne '0.1.0-preview.1') { throw 'Prerelease selection failed.' }
    $checks++
    Reject-Action { & (Join-Path $PSScriptRoot 'Get-DriverReleaseVersion.ps1') -ManifestPath $manifest -Tag 'v0.2.0' -RequireTag }
    Reject-Action { & (Join-Path $PSScriptRoot 'Get-DriverReleaseVersion.ps1') -ManifestPath $manifest -Tag 'master' -RequireTag }
    $package=Join-Path $fixture 'Example.pkg'
    $zip=[IO.Compression.ZipFile]::Open($package,[IO.Compression.ZipArchiveMode]::Create)
    try { $writer=[IO.StreamWriter]::new($zip.CreateEntry('Example.dat').Open()); try{$writer.Write('{"driverVersion":"0.1.0.0"}')}finally{$writer.Dispose()} }finally{$zip.Dispose()}
    & (Join-Path $PSScriptRoot 'Assert-DriverPackageVersion.ps1') -PackagePath $package -ManifestPath $manifest -ReleaseVersion '0.1.0'
    $checks++
    Reject-Action { & (Join-Path $PSScriptRoot 'Assert-DriverPackageVersion.ps1') -PackagePath $package -ManifestPath $manifest -ReleaseVersion '0.2.0' }
    Write-Output "Release version policy: $checks checks passed. Working driver manifests were not changed."
} finally {
    $resolved=[IO.Path]::GetFullPath($fixture)
    if ([IO.Path]::GetDirectoryName($resolved).TrimEnd('\','/') -ne $temporaryRoot.TrimEnd('\','/') -or [IO.Path]::GetFileName($resolved) -notlike 'EaseeReleaseChecks-*') { throw 'Unexpected fixture cleanup path.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
