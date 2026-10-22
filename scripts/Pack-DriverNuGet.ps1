# Copyright (c) 2026 Neil Colvin. MIT License with Commons Clause; see LICENSE.
param([Parameter(Mandatory)][string]$DriverPackagePath,[Parameter(Mandatory)][string]$Version,[Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$DriverPackagePath=(Resolve-Path -LiteralPath $DriverPackagePath).Path
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
$null=[IO.Directory]::CreateDirectory($OutputDirectory)
& (Join-Path $PSScriptRoot 'Assert-DriverPackageVersion.ps1') -PackagePath $DriverPackagePath -ManifestPath (Join-Path $root 'EaseeCrestronDriver/EaseeCrestronDriver.json') -ReleaseVersion $Version
$manifest=Join-Path $OutputDirectory 'crestron-driver-package.json'
if(Test-Path -LiteralPath $manifest){throw 'Use a fresh NuGet staging directory.'}
[ordered]@{standardVersion='1.0';packageType='CrestronHomeDriver';driverPlatform='Crestron Home';payloadType='pkg';payloadFile='EaseeCrestronDriver.pkg';containsManagedDllPayload=$false;packageId='CrestronHomeDriver.Easee.Charger';packageVersion=$Version;displayName='Easee Charger Driver';vendor='Easee';driverCategory='Energy Automation';payloadSha256=(Get-FileHash -LiteralPath $DriverPackagePath).Hash.ToLowerInvariant()}|ConvertTo-Json|Set-Content -LiteralPath $manifest
& dotnet pack (Join-Path $root 'EaseeCrestronDriver/EaseeCrestronDriver.csproj') -c Release --no-build --no-restore --output $OutputDirectory ('-p:PackageVersion='+$Version) ('-p:PackageDriverPkgPath='+$DriverPackagePath) ('-p:PackageDriverManifestPath='+$manifest) ('-p:PackageReleaseNotesFile='+(Join-Path $root 'RELEASE-NOTES.md')) -p:BuildDriverPackage=false
if($LASTEXITCODE -ne 0){throw 'NuGet wrapper creation failed.'}
$package=Join-Path $OutputDirectory ('CrestronHomeDriver.Easee.Charger.'+$Version+'.nupkg')
& (Join-Path $PSScriptRoot 'Assert-DriverNuGetPackage.ps1') -PackagePath $package -DriverPackagePath $DriverPackagePath -Version $Version
