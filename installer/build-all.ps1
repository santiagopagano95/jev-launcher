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

Remove-Item $stage, $pkg, $payload, $dist -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $stage, (Join-Path $pkg 'app') | Out-Null

# 1) Publicar una sola vez.
Invoke-JevPublish -Output $stage -Version $Version

# 2) Inno payload = mismos bytes.
New-Item -ItemType Directory -Force -Path $payload | Out-Null
Copy-Item (Join-Path $stage '*') $payload -Recurse -Force

# 3) Paquete del ZIP = app + scripts.
Copy-Item (Join-Path $stage '*') (Join-Path $pkg 'app') -Recurse -Force
foreach ($file in 'install.ps1', 'uninstall.ps1', 'install.cmd', 'uninstall.cmd', 'README.txt') {
    Copy-Item (Join-Path $PSScriptRoot $file) $pkg -Force
}
New-Item -ItemType Directory -Force -Path $dist | Out-Null
$zip = Join-Path $dist "JevLauncher-$Version.zip"
Compress-Archive -Path (Join-Path $pkg '*') -DestinationPath $zip

# 4) Setup de Inno.
if (-not (Test-Path $Iscc)) {
    throw "No encontre ISCC.exe en '$Iscc'. Instala Inno Setup 6 (winget install JRSoftware.InnoSetup)."
}
& $Iscc "/DMyAppVersion=$Version" (Join-Path $PSScriptRoot 'inno\JevLauncher.iss')
if ($LASTEXITCODE -ne 0) { throw "ISCC fallo con codigo $LASTEXITCODE" }
$setup = Join-Path $dist "JevLauncher-Setup-$Version.exe"

# 5) Verificar que ZIP y Setup comparten los mismos binarios.
foreach ($file in 'JevLauncher.App.exe', 'JevLauncher.App.dll', 'JevLauncher.Core.dll') {
    $a = (Get-FileHash (Join-Path $stage $file) -Algorithm SHA256).Hash
    $b = (Get-FileHash (Join-Path $payload $file) -Algorithm SHA256).Hash
    $c = (Get-FileHash (Join-Path (Join-Path $pkg 'app') $file) -Algorithm SHA256).Hash
    if ($a -ne $b -or $a -ne $c) { throw "Los binarios no coinciden para $file" }
}

Write-Host "OK: ZIP y Setup comparten los mismos binarios."
Write-Host "ZIP:   $zip"
Write-Host "Setup: $setup"
