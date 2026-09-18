# Comandos `/` Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Add slash commands (`/web`, `/yt`, `/gh`, `/file`, `/recent`, …) that scope the local search or open a browser search URL, without touching the Jev path.

**Architecture:** A `Commands` registry plus `CommandParser` in Core; two new `CandidateKind`s (`Command`, `OpenUrl`); `Prefilter` gains an optional kind filter; `LauncherEngine` resolves command queries locally (no Jev). The App inserts command text and handles app actions.

**Tech Stack:** .NET 10, C#, xUnit, WPF.

**Design doc:** `docs/plans/2026-09-18-jev-launcher-commands-design.md`

---

## Task 1: Registry and parser

**Files:** Create `src/JevLauncher.Core/Commands.cs`; Test `tests/JevLauncher.Tests/CommandsTests.cs`

- Step 1: Tests for `CommandParser.Split` and `Resolve` (by id and alias, case-insensitive, unknown → null).
- Step 2: Run and verify FAIL.
- Step 3: Implement `CommandScope`, `LauncherCommand`, `Commands.All`, `CommandParser`.
- Step 4: Run and verify PASS.
- Step 5: Commit.

## Task 2: Candidate building

**Files:** Modify `src/JevLauncher.Core/Models.cs` (add `Command`, `OpenUrl`); Create `src/JevLauncher.Core/CommandCandidates.cs`; Test.

- Step 1: Tests: palette rows are `Command` with `Target` = `/id `; `/yt lofi` builds an `OpenUrl` with `youtube.com/results?search_query=lofi`; args are URL-escaped.
- Step 2: Verify FAIL.
- Step 3: Implement.
- Step 4: Verify PASS. Commit.

## Task 3: Scope filtering

**Files:** Modify `src/JevLauncher.Core/Prefilter.cs`; Test `LauncherPipelineTests`.

- Step 1: Test `/file` returns only `OpenFile`; `/app` only `OpenApp`.
- Step 2: Verify FAIL.
- Step 3: Add optional `CandidateKind? only` filter to `BuildCandidates`.
- Step 4: Verify PASS. Commit.

## Task 4: Engine integration

**Files:** Modify `src/JevLauncher.Core/LauncherEngine.cs`; Test.

- Step 1: Tests: `/yt lofi` → top `OpenUrl`; `/` → palette of `Command`; `/to` → filtered palette; `/file x` → only files; `/settings` → an app-action candidate; command queries never call the fake Jev.
- Step 2: Verify FAIL.
- Step 3: Implement command resolution in `Update` (and early-return in `UpdateAsync`).
- Step 4: Verify PASS. Commit.

## Task 5: Executor and Jev kind names

**Files:** Modify `src/JevLauncher.Core/Executor.cs`, `JevQuestions.cs`; Test.

- Step 1: Test `Executor` opens `OpenUrl` targets (pure: assert `JevQuestions.KindName` maps `open_url`/`command`).
- Step 2: Verify FAIL. Step 3: Implement. Step 4: PASS. Commit.

## Task 6: App wiring

**Files:** Modify `src/JevLauncher.App/PanelWindow.xaml.cs`

- Step 1: Enter on `Command` inserts `Target` into the query box and keeps the panel open.
- Step 2: App actions by candidate id: `app:settings` → OpenSettings, `app:quit` → quit, `app:help` → show palette.
- Step 3: `OpenUrl` goes through `Executor.Launch`.
- Step 4: Smoke: render `/` palette to `jev-commands.png`.

## Task 7: Verify and commit

- `dotnet test` all green; run `--smoke`; inspect the PNG; commit.
