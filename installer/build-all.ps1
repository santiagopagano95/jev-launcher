param(
    [string]$Version = "1.0.0",
    [string]$Iscc = "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'publish.ps1')

$dist = Join-Path $PSScriptRoot 'dist'
$stage = Join-Path $PSScriptRoot 'stage'
$pkg = Join-Path $PSScriptRoot 'stage-zip'
$payload = Join-Path $PSScriptRoot 'inno-payload'

Assert-JevIscc -Iscc $Iscc | Out-Null

Remove-JevDir $stage
Remove-JevDir $pkg
Remove-JevDir $payload
Remove-JevDir $dist
New-Item -ItemType Directory -Force -Path $stage, (Join-Path $pkg 'app') | Out-Null

# 1) Publicar una sola vez.
Invoke-JevPublish -Output $stage -Version $Version

# 2) Inno payload = mismos bytes.
New-Item -ItemType Directory -Force -Path $payload | Out-Null
Copy-Item (Join-Path $stage '*') $payload -Recurse -Force

# 3) Paquete del ZIP = app + scripts.
Copy-Item (Join-Path $stage '*') (Join-Path $pkg 'app') -Recurse -Force
foreach ($file in Get-JevInstallerFiles) {
    Copy-Item (Join-Path $PSScriptRoot $file) $pkg -Force
}
New-Item -ItemType Directory -Force -Path $dist | Out-Null
$zip = Join-Path $dist "JevLauncher-$Version.zip"
Compress-Archive -Path (Join-Path $pkg '*') -DestinationPath $zip

# 4) Setup de Inno.
Invoke-JevInnoSetup -Version $Version -Iscc $Iscc
$setup = Join-Path $dist "JevLauncher-Setup-$Version.exe"
if (-not (Test-Path $setup)) { throw "ISCC no genero $setup" }

# 5) Verificar que el ZIP real y el payload de Inno comparten los binarios de ejecucion.
$verify = Join-Path $env:TEMP ("jev-verify-" + [Guid]::NewGuid().ToString('N'))
try {
    Expand-Archive -Path $zip -DestinationPath $verify -Force
    foreach ($file in 'JevLauncher.App.exe', 'JevLauncher.App.dll', 'JevLauncher.Core.dll') {
        $zipHash = (Get-FileHash (Join-Path (Join-Path $verify 'app') $file) -Algorithm SHA256).Hash
        $payloadHash = (Get-FileHash (Join-Path $payload $file) -Algorithm SHA256).Hash
        if ($zipHash -ne $payloadHash) { throw "El ZIP y el Setup no coinciden para $file" }
    }
}
finally {
    Remove-JevDir $verify
}

Write-Host "OK: los binarios de ejecucion del ZIP y del Setup coinciden."
Write-Host "ZIP:   $zip"
Write-Host "Setup: $setup"
