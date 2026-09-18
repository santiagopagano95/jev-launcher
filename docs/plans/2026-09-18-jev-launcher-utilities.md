# Redes, servicios y utilidades — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans. Steps use checkbox syntax.

**Goal:** Add social/service web commands and local utilities (snippets, clipboard history, text tools, window switcher, notes, timer, Ctrl+Enter) on top of the existing slash-command engine.

**Architecture:** Extend the command registry and scopes; add pure Core helpers and a `LauncherServices` bundle to the engine; the App wires clipboard listening, timers, note saving and secondary actions.

**Design doc:** `docs/plans/2026-09-18-jev-launcher-utilities-design.md`

---

## Task 1: Registry — HomeUrl, scopes and new commands
Add `HomeUrl`, scopes `Snippets/Clipboard/Utility/Windows/Notes/Timer`, and the social/service/utility commands. Tests: every Web command has a `{0}` template; new commands resolve.

## Task 2: HomeUrl behaviour
`CommandCandidates.Home(command)` → `OpenUrl` with `HomeUrl`. Engine: Web command with empty arg and `HomeUrl` → home candidate. Tests.

## Task 3: UtilityCommands (pure)
`/uuid [n]`, `/b64`, `/b64d`, `/hash`, `/color` producing `Copy` candidates. Tests: uuid count/format, base64 round-trip, SHA-256 known value, color normalization (short + long, with/without `#`).

## Task 4: ClipboardHistory, NotesStore, snippets
`ClipboardHistory` (dedupe, newest-first, cap 50); `NotesStore` (append with timestamp, recent without timestamp); snippet/snippet filtering. Tests.

## Task 5: WindowList
`EnumWindows` enumeration excluding own process/invisible/untitled; `Focus(hwnd)` restores + foreground. Smoke-verified.

## Task 6: Engine wiring
Resolve `Snippets`, `Clipboard`, `Utility`, `Windows` (fuzzy-filtered), `Notes`, `Timer` scopes locally. Tests per scope.

## Task 7: Executor + kind names
`Copy` → clipboard; `FocusWindow` → focus. `KindName` maps the new kinds. Tests for kind mapping.

## Task 8: App wiring
Clipboard listener (`AddClipboardFormatListener` + `WM_CLIPBOARDUPDATE`), `Ctrl+Enter` copies path/URL, `Timer` scheduling + tray notification, `SaveNote` append, Settings snippets editor.

## Task 9: Verify and commit
`dotnet test` green; `--smoke` renders palette + utility; manual pass on clipboard/windows/timer; commit.
