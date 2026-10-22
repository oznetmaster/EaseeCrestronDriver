# Copyright (c) 2026 Neil Colvin. MIT License with Commons Clause; see LICENSE.
param([Parameter(Mandatory)][string]$Version,[Parameter(Mandatory)][string]$SdkRoot)
$ErrorActionPreference='Stop'
if($Version -notmatch '^\d+\.\d+\.\d+$'){throw 'Use a three-part test-host release version.'}
$root=Split-Path $PSScriptRoot -Parent
$lock=Get-Content -LiteralPath (Join-Path $root 'ProcessorTestSdk.lock.json') -Raw|ConvertFrom-Json
if((git -C $SdkRoot rev-parse HEAD).Trim() -cne $lock.revision){throw 'SDK differs from the locked public source.'}
$commit=(git -C $root rev-parse HEAD).Trim();if($LASTEXITCODE -ne 0){throw 'Cannot identify test source.'}
$project=Join-Path $root 'EaseeCrestronDriver.ProcessorTests/EaseeCrestronDriver.ProcessorTests.csproj'
& dotnet build $project -c Release --disable-build-servers -p:BuildDriverPackage=false -p:BuildProcessorTestPackages=true ('-p:ProcessorTestSdkRoot='+$SdkRoot) ('-p:ReleaseVersion='+$Version) ('-p:Version='+$Version) -p:DeployAfterBuild=false
if($LASTEXITCODE -ne 0){throw 'Processor test package build failed.'}
$directory=Split-Path $project -Parent
$package=Join-Path $directory 'bin/Release/net472/EaseeCrestronDriver.ProcessorTests.pkg'
$manifest=Join-Path $directory 'EaseeCrestronDriver.ProcessorTests.json'
& (Join-Path $PSScriptRoot 'Assert-DriverPackageVersion.ps1') -PackagePath $package -ManifestPath $manifest -ReleaseVersion $Version
$output=Join-Path $root 'artifacts/processor-release';if(Test-Path -LiteralPath $output){throw 'Use a fresh processor release directory.'};$null=[IO.Directory]::CreateDirectory($output)
Copy-Item -LiteralPath $package -Destination $output
$docs=[IO.Compression.ZipFile]::Open((Join-Path $output ('EaseeCrestronDriver.ProcessorTests-'+$Version+'-Documentation.zip')),[IO.Compression.ZipArchiveMode]::Create)
try{
 foreach($name in @('README.md','CHANGELOG.md','LICENSE','VALIDATION.md','PUBLISHING.md','ProcessorTestSdk.lock.json')){$null=[IO.Compression.ZipFileExtensions]::CreateEntryFromFile($docs,(Join-Path $root $name),$name)}
 $null=[IO.Compression.ZipFileExtensions]::CreateEntryFromFile($docs,$manifest,'ProcessorTestManifest.json')
 $null=[IO.Compression.ZipFileExtensions]::CreateEntryFromFile($docs,(Join-Path $root 'validation/Workflow/README.md'),'PROCESSOR-WORKFLOW.md')
 $hostPackage=[IO.Compression.ZipFile]::OpenRead($package)
 try{foreach($entry in $hostPackage.Entries|Where-Object {$_.FullName.StartsWith('Licenses/',[StringComparison]::OrdinalIgnoreCase)}){$licenceStream=$entry.Open();$target=$docs.CreateEntry($entry.FullName).Open();try{$licenceStream.CopyTo($target)}finally{$target.Dispose();$licenceStream.Dispose()}}}finally{$hostPackage.Dispose()}
}finally{$docs.Dispose()}
[ordered]@{Repository='oznetmaster/EaseeCrestronDriver';Commit=$commit;TestReleaseVersion=$Version;DriverNuGetPublished=$false;ProcessorSdkRevision=$lock.revision;PreparedTestManifestSha256=(Get-FileHash -LiteralPath $manifest).Hash.ToLowerInvariant()}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $output 'ProcessorTestSourceIdentity.json')
Get-ChildItem -LiteralPath $output -File|Sort-Object Name|ForEach-Object{(Get-FileHash -LiteralPath $_.FullName).Hash.ToLowerInvariant()+'  '+$_.Name}|Set-Content -LiteralPath (Join-Path $output 'PROCESSOR-TESTS-SHA256SUMS.txt')
'Staged processor-only assets; no driver or library package publication was performed.'
