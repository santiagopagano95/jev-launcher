param(
    [string]$Version = "1.0.0",
    [string]$Output = "$PSScriptRoot\dist"
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'publish.ps1')

$stage = Join-Path $Output 'JevLauncher'

Remove-JevDir $Output
New-Item -ItemType Directory -Force -Path (Join-Path $stage 'app') | Out-Null

Invoke-JevPublish -Output (Join-Path $stage 'app') -Version $Version

foreach ($file in Get-JevInstallerFiles) {
    Copy-Item (Join-Path $PSScriptRoot $file) $stage -Force
}

$zip = Join-Path $Output "JevLauncher-$Version.zip"
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip

Write-Host "ZIP generado: $zip"
Write-Host "Contenido: app\ + install.cmd + uninstall.cmd + README.txt"
