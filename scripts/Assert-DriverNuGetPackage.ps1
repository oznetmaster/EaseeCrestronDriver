# Copyright (c) 2026 Neil Colvin. MIT License with Commons Clause; see LICENSE.
param([Parameter(Mandatory)][string]$PackagePath,[Parameter(Mandatory)][string]$DriverPackagePath,[Parameter(Mandatory)][string]$Version)
$ErrorActionPreference='Stop'
$id='CrestronHomeDriver.Easee.Charger'
$zip=[IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $PackagePath))
function ReadEntry([string]$name){$entries=@($zip.Entries|Where-Object {$_.FullName -ceq $name});if($entries.Count -ne 1){throw ('Expected exactly one root '+$name)};$reader=[IO.StreamReader]::new($entries[0].Open());try{$reader.ReadToEnd()}finally{$reader.Dispose()}}
try{
 $names=@($zip.Entries|ForEach-Object FullName)
 if(@($names|Select-Object -Unique).Count -ne $names.Count -or @($names|Where-Object {$_ -match '(?i)(^|/)lib/|\.dll$|(^|/)\.\.?/|\\'}).Count){throw 'Duplicate, unsafe or library-payload entries are prohibited.'}
 $payloads=@($zip.Entries|Where-Object {$_.FullName.EndsWith('.pkg',[StringComparison]::OrdinalIgnoreCase)})
 if($payloads.Count -ne 1 -or $payloads[0].FullName -cne 'EaseeCrestronDriver.pkg'){throw 'Exactly one root EaseeCrestronDriver.pkg is required.'}
 $manifest=ReadEntry 'crestron-driver-package.json'|ConvertFrom-Json
 $fields=@{standardVersion='1.0';packageType='CrestronHomeDriver';driverPlatform='Crestron Home';payloadType='pkg';payloadFile='EaseeCrestronDriver.pkg';packageId=$id;packageVersion=$Version;displayName='Easee Charger Driver';vendor='Easee';driverCategory='Energy Automation'}
 foreach($key in $fields.Keys){if($manifest.$key -cne $fields[$key]){throw ('Incorrect manifest field: '+$key)}}
 if($manifest.containsManagedDllPayload -isnot [bool] -or $manifest.containsManagedDllPayload){throw 'containsManagedDllPayload must be false.'}
 $stream=$payloads[0].Open();try{$hash=[Convert]::ToHexStringLower([Security.Cryptography.SHA256]::HashData($stream))}finally{$stream.Dispose()}
 $expected=(Get-FileHash -LiteralPath $DriverPackagePath -Algorithm SHA256).Hash.ToLowerInvariant()
 if($hash -cne $expected -or $manifest.payloadSha256 -cne $expected){throw 'NuGet payload differs from the verified Release driver.'}
 $null=ReadEntry 'README.md';$null=ReadEntry 'LICENSE'
 $nuspecs=@($zip.Entries|Where-Object {$_.FullName -notmatch '/' -and $_.FullName.EndsWith('.nuspec',[StringComparison]::OrdinalIgnoreCase)})
 if($nuspecs.Count -ne 1){throw 'Expected one root nuspec.'}
 [xml]$spec=ReadEntry $nuspecs[0].FullName
 $metadata=$spec.package.metadata
 if($metadata.id -cne $id -or $metadata.version -cne $Version -or $metadata.packageTypes.packageType.name -cne 'CrestronHomeDriver'){throw 'Incorrect NuGet identity, version or package type.'}
 if($metadata.readme -cne 'README.md' -or $metadata.license.type -cne 'file' -or $metadata.license.InnerText -cne 'LICENSE'){throw 'Required readme/license metadata missing.'}
 if($metadata.repository.type -cne 'git' -or $metadata.repository.url -cne 'https://github.com/oznetmaster/EaseeCrestronDriver' -or $metadata.projectUrl -cne 'https://github.com/oznetmaster/EaseeCrestronDriver' -or $metadata.authors -cne 'Neil Colvin'){throw 'Required repository/author metadata missing.'}
 foreach($tag in @('crestron','crestron-home','driver','pkg')){if($tag -notin @($metadata.tags -split '[;,\s]+')){throw ('Missing tag: '+$tag)}}
 if($metadata.description -notmatch '(?i)final\s+\.pkg' -or @($metadata.SelectNodes('.//*[local-name()="dependency"]')).Count){throw 'The wrapper must describe a final .pkg payload and declare no dependencies.'}
 Write-Host "Verified $id $Version against Crestron Home Driver NuGet Publishing Standard v1; embedded driver SHA256 $hash."
}finally{$zip.Dispose()}
