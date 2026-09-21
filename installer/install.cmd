@echo off
rem Instala Jev Launcher (sin admin).
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1" %*
pause
