@echo off
rem Starts the AutoHotkey hotkey helper without relying on .ahk file association.
setlocal
set "AHK=C:\Program Files\AutoHotkey\v2\AutoHotkey.exe"
if not exist "%AHK%" set "AHK=C:\Program Files\AutoHotkey\AutoHotkey.exe"
if not exist "%AHK%" (
  echo AutoHotkey v2 is not installed. Get it from https://www.autohotkey.com/
  pause
  exit /b 1
)
start "" "%AHK%" "%~dp0jev-launcher.ahk"
