# Store apps — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans.

**Goal:** Index Store/UWP apps (and classic apps without a Start Menu shortcut) via `shell:AppsFolder`.

**Design doc:** `docs/plans/2026-09-19-jev-launcher-store-apps-design.md`

## Task 1: StoreApps (pure parts + COM enumeration)
`src/JevLauncher.Core/StoreApps.cs`: `StoreApp(Name, Path)`, `IsNoise`, `ToCandidate`,
`ToCandidates`, `Enumerate` (COM late binding). Tests for the pure parts.

## Task 2: Index integration
`LocalIndex.Build()` adds `StoreApps.ToCandidates(StoreApps.Enumerate(), startMenuTitles)`.
Test: dedupe helper skips titles already present.

## Task 3: Verify
`dotnet test`; `--smoke` reports the count and resolves `/app notepad`; real check opening a Store app; commit.
