param(
    [string]$InstallDir = "$env:LOCALAPPDATA\Programs\JevLauncher",
    [string]$Version = "1.0.0",
    [switch]$SkipShortcut,
    [switch]$SkipRegistry,
    [switch]$NoLaunch,
    [switch]$SkipProcessKill
)

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$source = Join-Path $here 'app'

if (-not (Test-Path $source)) {
    throw "No encontre la carpeta 'app' junto al instalador. Descomprimi el ZIP completo."
}

# Cerrar cualquier instancia corriendo.
if (-not $SkipProcessKill) {
    Get-Process JevLauncher.App -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 300
}

New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
Copy-Item (Join-Path $source '*') $InstallDir -Recurse -Force
Copy-Item (Join-Path $here 'uninstall.ps1') $InstallDir -Force

$exe = Join-Path $InstallDir 'JevLauncher.App.exe'

if (-not $SkipShortcut) {
    $startMenu = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
    New-Item -ItemType Directory -Force -Path $startMenu | Out-Null
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut((Join-Path $startMenu 'Jev Launcher.lnk'))
    $shortcut.TargetPath = $exe
    $shortcut.WorkingDirectory = $InstallDir
    $shortcut.IconLocation = $exe
    $shortcut.Save()
}

if (-not $SkipRegistry) {
    $key = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\JevLauncher'
    New-Item -Path $key -Force | Out-Null
    Set-ItemProperty $key DisplayName 'Jev Launcher'
    Set-ItemProperty $key DisplayVersion $Version
    Set-ItemProperty $key Publisher 'Jev Launcher'
    Set-ItemProperty $key InstallLocation $InstallDir
    Set-ItemProperty $key DisplayIcon $exe
    Set-ItemProperty $key UninstallString "powershell -NoProfile -ExecutionPolicy Bypass -File `"$InstallDir\uninstall.ps1`""
    Set-ItemProperty $key NoModify 1 -Type DWord
    Set-ItemProperty $key NoRepair 1 -Type DWord
}

if (-not $NoLaunch) { Start-Process $exe }

Write-Host "Jev Launcher instalado en: $InstallDir"
