# Desktop app URI — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans.

**Goal:** `/spotify` opens the Spotify desktop app when its URI scheme is registered.

**Design doc:** `docs/plans/2026-09-19-jev-launcher-desktop-uri-design.md`

## Task 1: Protocols helper
`src/JevLauncher.Core/Protocols.cs`: `IsRegistered(scheme)` via `Registry.ClassesRoot`.

## Task 2: Command fields and candidates
`LauncherCommand`: `DesktopUri`, `DesktopHome`, `Scheme`. `CommandCandidates.DesktopSearch`/`DesktopHome`. Tests for URI building/escaping.

## Task 3: Engine decision
`LauncherServices.IsProtocolRegistered`; the Web scope prefers the desktop URI when the scheme is registered, else the web URL (search and home). Tests with an injected detector.

## Task 4: Register Spotify
`/spotify` gets `DesktopUri = "spotify:search:{0}"` and `DesktopHome = "spotify:"`.

## Task 5: Verify
`dotnet test`, `--smoke`, real check opening the app, commit.
