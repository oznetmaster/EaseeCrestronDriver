# Copyright (c) 2026 Neil Colvin. MIT License with Commons Clause; see LICENSE.
param([Parameter(Mandatory)][string]$PackagePath,[Parameter(Mandatory)][string]$DriverPackagePath,[Parameter(Mandatory)][string]$Version)
$ErrorActionPreference='Stop'
$validator=Join-Path $PSScriptRoot 'Assert-DriverNuGetPackage.ps1'
& $validator -PackagePath $PackagePath -DriverPackagePath $DriverPackagePath -Version $Version
$temp=Join-Path ([IO.Path]::GetTempPath()) ('easee-nuget-policy-'+[guid]::NewGuid().ToString('N'));$null=[IO.Directory]::CreateDirectory($temp)
foreach($case in @('manifest-version','library-dll','changed-payload','wrong-package-type')){
 $copy=Join-Path $temp ($case+'.nupkg');Copy-Item -LiteralPath $PackagePath -Destination $copy
 $zip=[IO.Compression.ZipFile]::Open($copy,[IO.Compression.ZipArchiveMode]::Update)
 try{
  $entryName=switch($case){'manifest-version'{'crestron-driver-package.json'};'library-dll'{'lib/net472/Unexpected.dll'};'changed-payload'{'EaseeCrestronDriver.pkg'};'wrong-package-type'{($zip.Entries|Where-Object {$_.FullName.EndsWith('.nuspec',[StringComparison]::OrdinalIgnoreCase)}).FullName}}
  $entry=$zip.GetEntry($entryName);$text='unexpected payload'
  if($case -in @('manifest-version','wrong-package-type')){$reader=[IO.StreamReader]::new($entry.Open());try{$text=$reader.ReadToEnd()}finally{$reader.Dispose()}}
  if($entry){$entry.Delete()}
  if($case -eq 'manifest-version'){$value=$text|ConvertFrom-Json;$value.packageVersion='999.0.0';$text=$value|ConvertTo-Json}
  if($case -eq 'wrong-package-type'){$text=$text.Replace('name="CrestronHomeDriver"','name="Dependency"')}
  $writer=[IO.StreamWriter]::new($zip.CreateEntry($entryName).Open());try{$writer.Write($text)}finally{$writer.Dispose()}
 }finally{$zip.Dispose()}
 $rejected=$false;try{& $validator -PackagePath $copy -DriverPackagePath $DriverPackagePath -Version $Version}catch{$rejected=$true}
 if(!$rejected){throw ('NuGet policy accepted corrupt case: '+$case)}
}
'NuGet package validation: valid package accepted; four corrupt package cases rejected.'
