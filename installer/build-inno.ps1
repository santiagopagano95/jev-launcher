param(
    [string]$Version = "1.0.0",
    [string]$Iscc = "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'publish.ps1')

$payload = Join-Path $PSScriptRoot 'inno-payload'

Assert-JevIscc -Iscc $Iscc | Out-Null
Remove-JevDir $payload
New-Item -ItemType Directory -Force -Path $payload | Out-Null

Invoke-JevPublish -Output $payload -Version $Version

Invoke-JevInnoSetup -Version $Version -Iscc $Iscc

$setup = Join-Path $PSScriptRoot "dist\JevLauncher-Setup-$Version.exe"
if (-not (Test-Path $setup)) { throw "ISCC no genero $setup" }
Write-Host "Instalador generado: $setup"
