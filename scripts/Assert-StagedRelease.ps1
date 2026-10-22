# Copyright (c) 2026 Neil Colvin. MIT License with Commons Clause; see LICENSE.
param([Parameter(Mandatory)][string]$Directory,[Parameter(Mandatory)][string]$Version,[Parameter(Mandatory)][string]$Commit,[string]$BuildRunId='')
$ErrorActionPreference='Stop'
$expected=@('EaseeCrestronDriver.pkg','EaseeCrestronDriver.ProcessorTests.pkg',('EaseeCrestronDriver-'+$Version+'-Documentation.zip'),'SourceIdentity.json','crestron-driver-package.json',('CrestronHomeDriver.Easee.Charger.'+$Version+'.nupkg'))
$files=@(Get-ChildItem -LiteralPath $Directory -File|ForEach-Object Name)
if($files.Count -ne $expected.Count+1 -or @($files|Where-Object {$_ -notin ($expected+'SHA256SUMS.txt')}).Count){throw 'Unexpected or missing staged release assets.'}
$seen=@();foreach($line in Get-Content -LiteralPath (Join-Path $Directory 'SHA256SUMS.txt')){
 if($line -notmatch '^(?<hash>[a-fA-F0-9]{64})  (?<name>[^/\\]+)$'){throw 'Unsafe checksum entry.'}
 $name=$Matches.name;$hash=$Matches.hash
 if($name -notin $expected -or $name -in $seen){throw 'Unexpected or duplicate checksum entry.'}
 if((Get-FileHash -LiteralPath (Join-Path $Directory $name)).Hash -ine $hash){throw ('Checksum mismatch: '+$name)}
 $seen+=$name
}
if($seen.Count -ne $expected.Count){throw 'Missing staged checksum.'}
$identity=Get-Content -LiteralPath (Join-Path $Directory 'SourceIdentity.json') -Raw|ConvertFrom-Json
if($identity.Repository -cne 'oznetmaster/EaseeCrestronDriver' -or $identity.Commit -cne $Commit -or $identity.ReleaseVersion -cne $Version -or $identity.Configuration -cne 'Release' -or ($BuildRunId -and [string]$identity.BuildRunId -cne $BuildRunId)){throw 'Staged source/build identity does not match the release.'}
& (Join-Path $PSScriptRoot 'Assert-DriverNuGetPackage.ps1') -PackagePath (Join-Path $Directory ('CrestronHomeDriver.Easee.Charger.'+$Version+'.nupkg')) -DriverPackagePath (Join-Path $Directory 'EaseeCrestronDriver.pkg') -Version $Version
$driverManifest=Join-Path (Split-Path $PSScriptRoot -Parent) 'EaseeCrestronDriver/EaseeCrestronDriver.json'
& (Join-Path $PSScriptRoot 'Assert-DriverPackageVersion.ps1') -PackagePath (Join-Path $Directory 'EaseeCrestronDriver.pkg') -ManifestPath $driverManifest -ReleaseVersion $Version
'Staged release source, versions, checksums and NuGet payload verified.'
