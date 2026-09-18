@echo off
rem Starts Jev Launcher directly (it registers Alt+Space itself, no AutoHotkey needed).
rem Change the path below if you build to Release.
setlocal
set "EXE=%~dp0src\JevLauncher.App\bin\Debug\net10.0-windows\JevLauncher.App.exe"
if not exist "%EXE%" (
  echo Launcher not found:`n  %EXE%
  echo Build it first with:  dotnet build
  pause
  exit /b 1
)
start "" "%EXE%"
