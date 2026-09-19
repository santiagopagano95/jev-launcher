# Resident launcher — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans.

**Goal:** Keep the launcher available at all times: start with Windows, relaunching shows the panel, and the panel is never closed by accident.

**Design doc:** `docs/plans/2026-09-19-jev-launcher-resident-design.md`

## Task 1: Autostart
`src/JevLauncher.App/Autostart.cs` (`IsEnabled`, `Set`). `Settings.StartWithWindows` (default true). Apply on startup and on settings save.

## Task 2: Settings UI
Add the "Start with Windows" checkbox to `SettingsWindow.xaml` and wire it in the code-behind.

## Task 3: Relaunch shows the panel
`App.ClaimSingleInstance`: two named events (`ShowSignal`, `ToggleSignal`); first instance waits on both; second instance signals show/toggle and exits.

## Task 4: Prevent accidental close
`PanelWindow`: `Closing` cancels and hides unless `PrepareForExit()` was called; quit paths (tray, `/quit`, smoke) call it.

## Task 5: Verify
`dotnet test`; real check: start the app, launch it again, confirm `ShowPanel` in the debug log; confirm the Run key is written; commit.
