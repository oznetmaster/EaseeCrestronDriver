# Copyright (c) 2026 Neil Colvin. MIT License with Commons Clause; see LICENSE.
param([Parameter(Mandatory)][string]$Directory,[Parameter(Mandatory)][string]$Version)
$ErrorActionPreference='Stop'
if([string]::IsNullOrWhiteSpace($env:NUGET_TEMP_KEY)){throw 'NuGet OIDC did not issue a temporary credential.'}
if($Version -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$'){throw 'Invalid release version.'}
$name='CrestronHomeDriver.Easee.Charger.'+$Version+'.nupkg'
$package=Join-Path $Directory $name;$driver=Join-Path $Directory 'EaseeCrestronDriver.pkg'
& (Join-Path $PSScriptRoot 'Assert-DriverNuGetPackage.ps1') -PackagePath $package -DriverPackagePath $driver -Version $Version
$uri='https://api.nuget.org/v3-flatcontainer/crestronhomedriver.easee.charger/'+$Version.ToLowerInvariant()+'/'+$name.ToLowerInvariant()
$existing=Join-Path ([IO.Path]::GetTempPath()) ([guid]::NewGuid().ToString('N')+'.nupkg')
$response=Invoke-WebRequest -Uri $uri -OutFile $existing -PassThru -SkipHttpErrorCheck -TimeoutSec 30
if($response.StatusCode -eq 200){
 & (Join-Path $PSScriptRoot 'Assert-DriverNuGetPackage.ps1') -PackagePath $existing -DriverPackagePath $driver -Version $Version
 'This NuGet version already contains the verified driver payload; publication is complete.'
}elseif($response.StatusCode -eq 404){
 & dotnet nuget push $package --api-key $env:NUGET_TEMP_KEY --source https://api.nuget.org/v3/index.json
 if($LASTEXITCODE -ne 0){throw 'NuGet publication failed.'}
}else{throw ('Cannot establish NuGet version availability; HTTP '+$response.StatusCode)}
