param(
    [string]$InstallDir = "$env:LOCALAPPDATA\Programs\JevLauncher",
    [switch]$KeepData,
    [switch]$SkipShortcut,
    [switch]$SkipRegistry,
    [switch]$SkipProcessKill
)

$ErrorActionPreference = 'Continue'

if (-not $SkipProcessKill) {
    Get-Process JevLauncher.App -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 300
}

if (-not $SkipRegistry) {
    Remove-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'JevLauncher' -ErrorAction SilentlyContinue
    Remove-Item 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\JevLauncher' -Recurse -Force -ErrorAction SilentlyContinue
}

if (-not $SkipShortcut) {
    Remove-Item (Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Jev Launcher.lnk') -Force -ErrorAction SilentlyContinue
}

if (-not $KeepData) {
    Remove-Item (Join-Path $env:APPDATA 'JevLauncher') -Recurse -Force -ErrorAction SilentlyContinue
}

Remove-Item $InstallDir -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "Jev Launcher desinstalado."
