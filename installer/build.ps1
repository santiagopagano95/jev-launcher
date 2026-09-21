param(
    [string]$Version = "1.0.0",
    [string]$Output = "$PSScriptRoot\dist"
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$stage = Join-Path $Output 'JevLauncher'

Remove-Item $Output -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path (Join-Path $stage 'app') | Out-Null

dotnet publish (Join-Path $root 'src\JevLauncher.App\JevLauncher.App.csproj') `
    -c Release -r win-x64 --self-contained false -p:Version=$Version `
    -o (Join-Path $stage 'app')

foreach ($file in 'install.ps1', 'uninstall.ps1', 'install.cmd', 'uninstall.cmd', 'README.txt') {
    Copy-Item (Join-Path $PSScriptRoot $file) $stage -Force
}

$zip = Join-Path $Output "JevLauncher-$Version.zip"
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip

Write-Host "ZIP generado: $zip"
Write-Host "Contenido: app\ + install.cmd + uninstall.cmd + README.txt"
