# Web results (Brave) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans.

**Goal:** `/web <query>` shows Brave results inline, falling back to a browser action when there is no key.

**Design doc:** `docs/plans/2026-09-18-jev-launcher-websearch-design.md`

---

## Task 1: Brave client and parser
Create `src/JevLauncher.Core/WebSearch.cs` (`WebResult`, `IWebSearch`, `WebSearchResponse.Parse`, `BraveWebSearch`). Test `WebSearchResponse.Parse` with a sample payload (titles/urls/descriptions, skips entries without url, empty on `{}`).

## Task 2: Result candidates
`WebResultCandidates.Build` → `OpenUrl` rows `web:i` with `domain · snippet` detail and URL target. Tests.

## Task 3: Command registry
`CommandScope.WebResults`; `/web` + aliases `search`/`s` move to it (keep `UrlTemplate` for the fallback). Tests: resolve aliases, template still present.

## Task 4: Engine
`LauncherServices.WebSearch`; `Update` returns the browser action for `/web x`; `UpdateAsync` fetches with stale discard + per-query cache, then appends the browser action. Tests with a fake `IWebSearch`: inline results, fallback when provider is null, stale response discarded.

## Task 5: Settings and App
`EncryptedBraveKey` + `GetBraveApiKey`/`SetBraveApiKey` (DPAPI); Settings field + *Test*; App builds `BraveWebSearch` and refreshes on settings change.

## Task 6: Verify
`dotnet test`, `--smoke` (reports whether web search is configured), normal run, commit.
