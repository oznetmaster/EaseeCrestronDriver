# Copyright (c) 2026 Neil Colvin. MIT License with Commons Clause; see LICENSE.
param([Parameter(Mandatory)][string]$Version, [Parameter(Mandatory)][string]$SdkRoot)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$selected = & (Join-Path $PSScriptRoot 'Get-DriverReleaseVersion.ps1') -ManifestPath (Join-Path $root 'EaseeCrestronDriver/EaseeCrestronDriver.json') -Tag ('v' + $Version) -RequireTag
$pin = Get-Content -LiteralPath (Join-Path $root 'ProcessorTestSdk.lock.json') -Raw | ConvertFrom-Json
$sdkRevision = (& git -C $SdkRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $sdkRevision -cne $pin.revision) { throw 'Processor SDK revision differs from the locked public source.' }
$commit = (& git -C $root rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot identify release source.' }
$output = Join-Path $root 'artifacts/release'
if (Test-Path -LiteralPath $output) { throw 'Use a fresh release staging directory.' }
$null = [IO.Directory]::CreateDirectory($output)
$packages = @(
    @{Name='EaseeCrestronDriver'; Processor=$false},
    @{Name='EaseeCrestronDriver.ProcessorTests'; Processor=$true}
)
foreach ($package in $packages) {
    $project = Join-Path $root ($package.Name + '/' + $package.Name + '.csproj')
    $arguments = @('build', $project, '-c', 'Release', '--disable-build-servers', '-p:DeployAfterBuild=false')
    if ($package.Processor) { $arguments += @('-p:BuildProcessorTestPackages=true', ('-p:ProcessorTestSdkRoot=' + $SdkRoot), ('-p:ReleaseVersion=' + $selected.DriverReleaseVersion)) }
    else { $arguments += '-p:BuildDriverPackage=true' }
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw ('Release build failed: ' + $package.Name) }
    $path = Join-Path $root ($package.Name + '/bin/Release/net472/' + $package.Name + '.pkg')
    $manifest = Join-Path $root ($package.Name + '/' + $package.Name + '.json')
    & (Join-Path $PSScriptRoot 'Assert-DriverPackageVersion.ps1') -PackagePath $path -ManifestPath $manifest -ReleaseVersion $Version
    Copy-Item -LiteralPath $path -Destination $output
}
$documentation = Join-Path $output ('EaseeCrestronDriver-' + $Version + '-Documentation.zip')
$zip = [IO.Compression.ZipFile]::Open($documentation, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($name in @('README.md','CHANGELOG.md','RELEASE-NOTES.md','LICENSE','VALIDATION.md','PUBLISHING.md','ProcessorTestSdk.lock.json')) {
        $null = [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, (Join-Path $root $name), $name)
    }
    $driver = [IO.Compression.ZipFile]::OpenRead((Join-Path $output 'EaseeCrestronDriver.pkg'))
    try {
        foreach ($entry in $driver.Entries | Where-Object { $_.FullName.StartsWith('Licenses/', [StringComparison]::OrdinalIgnoreCase) }) {
            $source = $entry.Open(); $target = $zip.CreateEntry($entry.FullName).Open()
            try { $source.CopyTo($target) } finally { $target.Dispose(); $source.Dispose() }
        }
    } finally { $driver.Dispose() }
} finally { $zip.Dispose() }
[ordered]@{ Repository='oznetmaster/EaseeCrestronDriver'; Commit=$commit; ReleaseVersion=$Version; DriverVersion=($selected.DriverReleaseVersion+'.0'); ProcessorSdkRevision=$sdkRevision; HardwareGate='Exact source check; independently rebuilt Release bytes require separate candidate validation.' } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'SourceIdentity.json')
$hashes = Get-ChildItem -LiteralPath $output -File | Sort-Object Name | ForEach-Object { (Get-FileHash -LiteralPath $_.FullName).Hash.ToLowerInvariant() + '  ' + $_.Name }
[IO.File]::WriteAllLines((Join-Path $output 'SHA256SUMS.txt'), $hashes)
