#Requires AutoHotkey v2.0
#SingleInstance Force

; ---------------------------------------------------------------------------
; Jev Launcher — global hotkey via AutoHotkey
;
; Alt+Space toggles the launcher panel. The app is single-instance, so running
; it again with --toggle simply signals the instance that is already running.
; --no-hotkey stops the app from registering Alt+Space itself, so AHK is the
; only owner of the shortcut.
;
; Adjust `launcher` below if you build to a different configuration.
; ---------------------------------------------------------------------------

launcher := A_ScriptDir "\src\JevLauncher.App\bin\Debug\net10.0-windows\JevLauncher.App.exe"

if !FileExist(launcher) {
    MsgBox "Launcher not found:`n" launcher "`n`nBuild it with: dotnet build", "Jev Launcher", "Iconx"
    ExitApp
}

!Space::Run('"' launcher '" --toggle --no-hotkey')
