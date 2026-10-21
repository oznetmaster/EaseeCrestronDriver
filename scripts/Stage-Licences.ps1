param([Parameter(Mandatory)][string]$AssetsFile, [Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$assets=Get-Content -LiteralPath $AssetsFile -Raw | ConvertFrom-Json -AsHashtable
$records=[Collections.Generic.List[object]]::new()
foreach($entry in $assets.libraries.GetEnumerator()) {
    if($entry.Value.type -ne 'package'){continue}
    $name=$entry.Key
    foreach($folder in $assets.packageFolders.Keys) {
        $package=Join-Path $folder $entry.Value.path
        if(!(Test-Path -LiteralPath $package)){continue}
        foreach($file in Get-ChildItem -LiteralPath $package -File | Where-Object { $_.Name -match '^(LICENSE|LICENCE|COPYING|NOTICE|THIRD.PARTY)' }) {
            $hash=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            $relative=$hash.Substring(0,20)+'.txt'
            [IO.Directory]::CreateDirectory($OutputDirectory)|Out-Null
            $destination=Join-Path $OutputDirectory $relative
            if(Test-Path -LiteralPath $destination) {
                if((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash.ToLowerInvariant() -cne $hash){throw 'Licence filename collision.'}
            } else { Copy-Item -LiteralPath $file.FullName -Destination $destination }
            $records.Add([ordered]@{package=$name;sourceFile=$file.Name;file=$relative;sha256=$hash})
        }
        break
    }
}
$index=Join-Path $OutputDirectory 'INDEX.json'
[IO.File]::WriteAllText($index,($records|ConvertTo-Json -Depth 5),[Text.UTF8Encoding]::new($false))
