param(
    [string]$Version = "1.0.0",
    [string]$Output = "$PSScriptRoot\dist"
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'publish.ps1')

$stage = Join-Path $Output 'JevLauncher'

Remove-Item $Output -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path (Join-Path $stage 'app') | Out-Null

Invoke-JevPublish -Output (Join-Path $stage 'app') -Version $Version

foreach ($file in 'install.ps1', 'uninstall.ps1', 'install.cmd', 'uninstall.cmd', 'README.txt') {
    Copy-Item (Join-Path $PSScriptRoot $file) $stage -Force
}

$zip = Join-Path $Output "JevLauncher-$Version.zip"
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip

Write-Host "ZIP generado: $zip"
Write-Host "Contenido: app\ + install.cmd + uninstall.cmd + README.txt"
