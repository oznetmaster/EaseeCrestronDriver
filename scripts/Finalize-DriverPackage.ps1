param([Parameter(Mandatory)][string]$PackagePath,[Parameter(Mandatory)][string]$AssemblyPath)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[IO.Compression.ZipFile]::Open($PackagePath,[IO.Compression.ZipArchiveMode]::Update)
try {
    $names=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach($entry in @($archive.Entries)) {
        $name=$entry.FullName.Replace('\','/')
        if($name.StartsWith('/') -or $name.Contains(':') -or $name.Split('/') -contains '..' -or !$names.Add($name)){throw 'Invalid or duplicate package path.'}
        if($name -cne $entry.FullName) {
            $replacement=$archive.CreateEntry($name,[IO.Compression.CompressionLevel]::Optimal)
            $source=$entry.Open();$destination=$replacement.Open()
            try{$source.CopyTo($destination)}finally{$destination.Dispose();$source.Dispose()}
            $entry.Delete()
        }
    }
}finally{$archive.Dispose()}
$archive=[IO.Compression.ZipFile]::OpenRead($PackagePath)
function EntryBytes([string]$name) {
    $entry=$archive.Entries|Where-Object FullName -IEQ $name
    if(@($entry).Count -ne 1){throw ('Required package file missing: '+$name)}
    $source=$entry.Open();$memory=[IO.MemoryStream]::new()
    try{$source.CopyTo($memory);return ,$memory.ToArray()}finally{$memory.Dispose();$source.Dispose()}
}
try {
    $dllName=[IO.Path]::GetFileName($AssemblyPath)
    $packaged=EntryBytes $dllName
    $hash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($packaged))
    if($hash -cne (Get-FileHash -LiteralPath $AssemblyPath -Algorithm SHA256).Hash){throw 'Packaged assembly differs from the patched driver.'}
    foreach($name in @('uidefinitions/UiDefinition.xml','translations/en-US.json','Licenses/EaseeDriver-LICENSE.txt')){$null=EntryBytes $name}
    $index=[Text.Encoding]::UTF8.GetString((EntryBytes 'Licenses/Dependencies/INDEX.json'))|ConvertFrom-Json
    foreach($licence in $index){
        $bytes=EntryBytes ('Licenses/Dependencies/'+$licence.file)
        $actual=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
        if($actual -cne $licence.sha256){throw 'A dependency licence was changed during packaging.'}
    }
    Write-Host ('Verified package closure, portable paths and '+@($index).Count+' dependency licence records.')
}finally{$archive.Dispose()}
