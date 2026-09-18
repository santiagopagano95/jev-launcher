# TypeSafe Restyle Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Re-skin the launcher panel and settings window to match the typesafe.ai aesthetic (dark `#1E1E1E`, Inter + JetBrains Mono, pink→magenta accent gradient) without touching launcher logic.

**Architecture:** Add bundled font resources and a shared WPF `ResourceDictionary` of color/brush/font tokens; the panel and settings XAML bind to those tokens. A `--smoke` step renders the panel to a PNG for visual verification.

**Tech Stack:** .NET 10, WPF, Inter + JetBrains Mono (SIL OFL), System.Text.Json (unchanged).

**Design doc:** `docs/plans/2026-09-18-jev-launcher-typesafe-restyle-design.md`

---

## Task 1: Bundle fonts

**Files:**
- Create: `src/JevLauncher.App/Fonts/Inter-Regular.ttf`, `Inter-Medium.ttf`, `Inter-SemiBold.ttf`
- Create: `src/JevLauncher.App/Fonts/JetBrainsMono-Regular.ttf`, `JetBrainsMono-Medium.ttf`
- Modify: `src/JevLauncher.App/JevLauncher.App.csproj`

**Step 1:** Download Inter and JetBrains Mono releases (SIL OFL) and extract the static Regular/Medium/SemiBold faces into `src/JevLauncher.App/Fonts/`.

**Step 2:** Add them to the csproj as `<Resource Include="Fonts\*.ttf" />`.

**Step 3:** Verify build; if any face is missing, fall back to Segoe UI Variable / Cascadia Mono for that role.

## Task 2: Design tokens

**Files:**
- Create: `src/JevLauncher.App/Theme.xaml`
- Modify: `src/JevLauncher.App/App.xaml`

**Step 1:** Define colors/brushes: `PanelBackground` `#1E1E1E`, `TextPrimary` `#FEFEFE`, `TextSecondary` `#DEDEDE`, `TextMuted` `#C4C4C4`, `TextHint` `#ABBAB9`, `AccentFrom` `#F386A1`, `AccentTo` `#D45BB6`, `ReadyGreen` `#03AA5C`, `Teal` `#09AEA1`, `Hairline` `rgba(255,255,255,0.08)`.

**Step 2:** Define the accent gradient brush and `FontFamily` keys (`UiFont` = Inter, `MonoFont` = JetBrains Mono) with system fallbacks.

**Step 3:** Merge `Theme.xaml` into `Application.Resources`.

## Task 3: Panel skin

**Files:**
- Modify: `src/JevLauncher.App/PanelWindow.xaml`
- Modify: `src/JevLauncher.App/PanelWindow.xaml.cs`

**Step 1:** Apply panel background, 1px hairline border, corner radius 14.

**Step 2:** Input in `UiFont` ~20px; add the 2px accent gradient underline; bind its opacity to an in-flight flag.

**Step 3:** Row template: title in `UiFont`, detail in `MonoFont` muted, left 2px gradient bar + `rgba(255,255,255,0.05)` on the selected row.

**Step 4:** Add a mono, lowercase kind label derived from `CandidateKind`.

**Step 5:** Ready badge `↵` in `ReadyGreen`; footer in `MonoFont`.

**Step 6:** Set the in-flight flag in `RunQueryAsync` (true before `UpdateAsync`, false after) and raise `PropertyChanged`.

## Task 4: Settings skin

**Files:**
- Modify: `src/JevLauncher.App/SettingsWindow.xaml`

**Step 1:** Apply panel background, `UiFont` labels, `MonoFont` for the hotkey field.
**Step 2:** Style the primary (Save) button with the accent gradient; secondary buttons neutral.

## Task 5: Visual verification

**Files:**
- Modify: `src/JevLauncher.App/PanelWindow.xaml.cs` (smoke render)

**Step 1:** In `RunSmokeAsync`, render the panel to `%TEMP%\jev-panel.png` with `RenderTargetBitmap`.
**Step 2:** Run `dotnet build` + `--smoke`; confirm the report still shows `Items=7`, calculator top, tray `IsCreated=True`, no crash log.
**Step 3:** Open the PNG and confirm the TypeSafe look (dark `#1E1E1E`, Inter titles, mono details, accent line).

## Task 6: Commit

**Step 1:** `dotnet test` (all green), `git add`, commit the restyle.
