# Jev Launcher Windows Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Port the macOS Jev Launcher to Windows as a WPF app: a Spotlight-style
panel (Alt+Space) that indexes local apps/files/toggles, sends each keystroke's
query plus candidates to Jev (TypeSafe) in one call, and re-ranks live.

**Architecture:** A UI-free `JevLauncher.Core` library holds all logic (index,
fuzzy, calculator, ranker, Jev client, executor, stats) behind plain .NET types,
so it is fully unit-testable with no network or UI. `JevLauncher.App` is a thin
WPF shell: frameless topmost window, global hotkey, tray icon, settings, and the
wiring that glues keystrokes to Core and results to rows.

**Tech Stack:** .NET 10, C#, WPF, xUnit, System.Text.Json, P/Invoke (user32,
powrprof, shell32, dwmapi, coreaudio COM interop).

**Design doc:** `docs/plans/2026-09-18-jev-launcher-windows-design.md`

---

## Task 0: Scaffold solution and git

**Files:**
- Create: `JevLauncher.sln`
- Create: `src/JevLauncher.Core/JevLauncher.Core.csproj`
- Create: `src/JevLauncher.App/JevLauncher.App.csproj`
- Create: `tests/JevLauncher.Tests/JevLauncher.Tests.csproj`
- Create: `.gitignore`

**Step 1: Verify the .NET SDK**

Run: `dotnet --list-sdks`
Expected: at least one SDK, e.g. `10.0.x`. If `No SDKs were found`, install it first:

```powershell
winget install Microsoft.DotNet.SDK.10
```

**Step 2: Initialize git and scaffold projects**

```powershell
git init
dotnet new sln -n JevLauncher
dotnet new classlib -n JevLauncher.Core -o src/JevLauncher.Core -f net10.0
dotnet new wpf      -n JevLauncher.App  -o src/JevLauncher.App  -f net10.0
dotnet new xunit    -n JevLauncher.Tests -o tests/JevLauncher.Tests -f net10.0
dotnet sln add src/JevLauncher.Core/JevLauncher.Core.csproj src/JevLauncher.App/JevLauncher.App.csproj tests/JevLauncher.Tests/JevLauncher.Tests.csproj
dotnet add src/JevLauncher.App/JevLauncher.App.csproj reference src/JevLauncher.Core/JevLauncher.Core.csproj
dotnet add tests/JevLauncher.Tests/JevLauncher.Tests.csproj reference src/JevLauncher.Core/JevLauncher.Core.csproj
```

**Step 3: Make Core Windows-targeted**

Edit `src/JevLauncher.Core/JevLauncher.Core.csproj` so the target framework is
`net10.0-windows` and nullable/implicit usings are on:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>
</Project>
```

Edit `tests/JevLauncher.Tests/JevLauncher.Tests.csproj` target framework to
`net10.0-windows` as well so it can reference Core.

**Step 4: Add .gitignore**

```gitignore
bin/
obj/
*.user
.vs/
build/
```

**Step 5: Verify build**

Run: `dotnet build`
Expected: `Build succeeded`.

**Step 6: Commit**

```powershell
git add -A
git commit -m "chore: scaffold solution and projects"
```

---

## Task 1: Core models

**Files:**
- Create: `src/JevLauncher.Core/Models.cs`

**Step 1: Write the models**

```csharp
namespace JevLauncher.Core;

public enum CandidateKind
{
    OpenApp,
    OpenFile,
    WebSearch,
    Calculate,
    SystemToggle,
    RunShortcut,
    Unclear,
}

public sealed record Candidate(
    string Id,
    CandidateKind Kind,
    string Title,
    string Detail,
    string Keywords,
    string? Target,
    double Fuzzy = 0);

public sealed record ScoredCandidate(
    Candidate Candidate,
    double Score,
    double TargetProbability,
    bool IsTopReady);

public sealed record LauncherContext(
    string FrontmostApp,
    IReadOnlyList<string> RecentApps,
    string ClipboardKind,
    string TimeOfDay,
    string Weekday);

public sealed record Conversation(IReadOnlyList<Candidate> Candidates, LauncherContext Context);
```

**Step 2: Build**

Run: `dotnet build src/JevLauncher.Core`
Expected: `Build succeeded`.

**Step 3: Commit**

```powershell
git add src/JevLauncher.Core/Models.cs
git commit -m "feat(core): add domain models"
```

---

## Task 2: Calculator

**Files:**
- Create: `src/JevLauncher.Core/Calculator.cs`
- Test: `tests/JevLauncher.Tests/CalculatorTests.cs`

**Step 1: Write the failing tests**

```csharp
using JevLauncher.Core;
using Xunit;

public class CalculatorTests
{
    [Theory]
    [InlineData("15% of 240", 36)]
    [InlineData("200 * 10%", 20)]
    [InlineData("2 + 2", 4)]
    [InlineData("2^3", 8)]
    [InlineData("sqrt 16", 4)]
    [InlineData("calc 1+1", 2)]
    [InlineData("= 3 * (4 + 1)", 15)]
    [InlineData("6 x 7", 42)]
    [InlineData("2 ** 3", 8)]
    public void Parses_arithmetic(string query, double expected)
    {
        Assert.True(Calculator.TryParse(query, out var value));
        Assert.Equal(expected, value, 6);
    }

    [Theory]
    [InlineData("the pdf I just downloaded")]
    [InlineData("dark")]
    [InlineData("")]
    [InlineData("wifi off")]
    public void Rejects_non_arithmetic(string query)
    {
        Assert.False(Calculator.TryParse(query, out _));
    }
}

public static class Calculator
{
}
```

> Note: remove the placeholder `Calculator` class from the test file once
> `Calculator.cs` exists. It is only there so the project compiles while the
> test file is added.

**Step 2: Run tests to verify they fail**

Run: `dotnet test --filter CalculatorTests`
Expected: FAIL (parse returns false for everything / class missing).

**Step 3: Write the calculator**

```csharp
using System.Globalization;

namespace JevLauncher.Core;

public static class Calculator
{
    public static bool TryParse(string query, out double value)
    {
        value = 0;
        var text = (query ?? string.Empty).Trim();
        if (text.Length == 0) return false;

        if (text.StartsWith("calc ", StringComparison.OrdinalIgnoreCase))
            text = text[5..].Trim();
        else if (text.StartsWith("=", StringComparison.Ordinal))
            text = text[1..].Trim();

        if (!LooksLikeMath(text)) return false;

        try
        {
            var parser = new Parser(Normalize(text));
            var result = parser.ParseExpression();
            parser.ExpectEnd();
            if (double.IsNaN(result) || double.IsInfinity(result)) return false;
            value = result;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool LooksLikeMath(string s)
    {
        var hasDigit = s.Any(char.IsDigit);
        var hasOperator = s.IndexOfAny(new[] { '+', '-', '*', '/', '^', '%', '(', ')' }) >= 0;
        return hasDigit && hasOperator;
    }

    private static string Normalize(string s)
    {
        var t = s.Replace("**", "^").Replace(" x ", "*").Replace("X", "*");
        // "15% of 240" -> "15% * 240" ; "200 * 10%" kept
        t = System.Text.RegularExpressions.Regex.Replace(t, @"%\s*of\s*", "%*", RegexOptions.IgnoreCase);
        t = System.Text.RegularExpressions.Regex.Replace(t, @"\bsqrt\b", "sqrt", RegexOptions.IgnoreCase);
        return t.Trim();
    }

    private sealed class Parser
    {
        private readonly string _s;
        private int _i;

        public Parser(string s) => _s = s;

        public void ExpectEnd()
        {
            SkipWs();
            if (_i != _s.Length) throw new FormatException();
        }

        public double ParseExpression()
        {
            var value = ParseTerm();
            while (true)
            {
                SkipWs();
                if (Peek('+')) { _i++; value += ParseTerm(); }
                else if (Peek('-')) { _i++; value -= ParseTerm(); }
                else return value;
            }
        }

        private double ParseTerm()
        {
            var value = ParseUnary();
            while (true)
            {
                SkipWs();
                if (Peek('*')) { _i++; value *= ParseUnary(); }
                else if (Peek('/')) { _i++; value /= ParseUnary(); }
                else return value;
            }
        }

        private double ParseUnary()
        {
            SkipWs();
            if (Peek('-')) { _i++; return -ParseUnary(); }
            if (Peek('+')) { _i++; return ParseUnary(); }
            return ParsePower();
        }

        private double ParsePower()
        {
            var left = ParseAtom();
            SkipWs();
            if (Peek('^')) { _i++; return Math.Pow(left, ParseUnary()); }
            return left;
        }

        private double ParseAtom()
        {
            SkipWs();
            if (Peek('('))
            {
                _i++;
                var v = ParseExpression();
                SkipWs();
                if (!Peek(')')) throw new FormatException();
                _i++;
                return ParsePostfixPercent(v);
            }

            if (MatchWord("sqrt"))
            {
                return ParsePostfixPercent(Math.Sqrt(ParseUnary()));
            }

            var start = _i;
            while (_i < _s.Length && (char.IsDigit(_s[_i]) || _s[_i] == '.')) _i++;
            if (start == _i) throw new FormatException();
            if (!double.TryParse(_s[start.._i], NumberStyles.Float, CultureInfo.InvariantCulture, out var num))
                throw new FormatException();
            return ParsePostfixPercent(num);
        }

        private double ParsePostfixPercent(double v)
        {
            SkipWs();
            if (Peek('%'))
            {
                _i++;
                return v / 100.0;
            }
            return v;
        }

        private bool MatchWord(string word)
        {
            SkipWs();
            if (_i + word.Length <= _s.Length &&
                string.Equals(_s.Substring(_i, word.Length), word, StringComparison.OrdinalIgnoreCase))
            {
                _i += word.Length;
                return true;
            }
            return false;
        }

        private void SkipWs()
        {
            while (_i < _s.Length && char.IsWhiteSpace(_s[_i])) _i++;
        }

        private bool Peek(char c) => _i < _s.Length && _s[_i] == c;
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test --filter CalculatorTests`
Expected: PASS.

**Step 5: Commit**

```powershell
git add src/JevLauncher.Core/Calculator.cs tests/JevLauncher.Tests/CalculatorTests.cs
git commit -m "feat(core): add arithmetic calculator with tests"
```

---

## Task 3: Fuzzy scorer

**Files:**
- Create: `src/JevLauncher.Core/Fuzzy.cs`
- Test: `tests/JevLauncher.Tests/FuzzyTests.cs`

**Step 1: Write the failing tests**

```csharp
using JevLauncher.Core;
using Xunit;

public class FuzzyTests
{
    [Fact]
    public void Exact_match_beats_prefix()
    {
        Assert.True(Fuzzy.Score("dark", "dark", "") > Fuzzy.Score("dark", "dark mode", ""));
    }

    [Fact]
    public void Prefix_beats_subsequence()
    {
        Assert.True(Fuzzy.Score("dar", "dark mode", "") > Fuzzy.Score("dar", "dashboard", ""));
    }

    [Fact]
    public void Word_initial_matches()
    {
        Assert.True(Fuzzy.Score("dm", "dark mode", "") > 0);
    }

    [Fact]
    public void Subsequence_matches_in_order_only()
    {
        Assert.True(Fuzzy.Score("dkm", "dark mode", "") > 0);
        Assert.Equal(0, Fuzzy.Score("mkd", "dark mode", ""));
    }

    [Fact]
    public void Keywords_are_searched()
    {
        Assert.True(Fuzzy.Score("wifi", "wireless", "wifi network") > 0);
    }

    [Fact]
    public void Stopwords_are_stripped()
    {
        Assert.Equal(Fuzzy.Score("the pdf", "report", "pdf"), Fuzzy.Score("pdf", "report", "pdf"));
    }

    [Fact]
    public void No_match_is_zero()
    {
        Assert.Equal(0, Fuzzy.Score("zzzz", "dark mode", "toggle"));
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test --filter FuzzyTests`
Expected: FAIL (Fuzzy type missing).

**Step 3: Write the scorer**

```csharp
using System.Text;

namespace JevLauncher.Core;

public static class Fuzzy
{
    private static readonly HashSet<string> Stopwords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "a", "an", "of", "to", "my", "i", "just", "and", "for", "in", "on",
        "is", "it", "this", "that", "please", "can", "you",
    };

    public static double Score(string query, string title, string keywords)
    {
        var q = Normalize(query);
        if (q.Length == 0) return 0;

        var titleNorm = Normalize(title);
        var keywordNorm = Normalize(keywords);

        return Math.Max(ScoreOne(q, titleNorm, isTitle: true), ScoreOne(q, keywordNorm, isTitle: false));
    }

    private static double ScoreOne(string q, string text, bool isTitle)
    {
        if (text.Length == 0) return 0;
        if (text == q) return 100;

        var weight = isTitle ? 1.0 : 0.9;

        if (text.StartsWith(q, StringComparison.Ordinal)) return 80 * weight;

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Any(w => w == q)) return 70 * weight;

        var initials = string.Concat(words.Select(w => w[0]));
        if (initials.Contains(q, StringComparison.Ordinal)) return 60 * weight;

        if (words.Any(w => w.StartsWith(q, StringComparison.Ordinal))) return 55 * weight;

        var sub = SubsequenceScore(q, text);
        return sub * weight;
    }

    private static double SubsequenceScore(string q, string text)
    {
        int qi = 0, gaps = 0;
        for (var ti = 0; ti < text.Length && qi < q.Length; ti++)
        {
            if (text[ti] == q[qi]) qi++;
            else if (qi > 0) gaps++;
        }
        if (qi < q.Length) return 0;
        return Math.Max(1, 40 - gaps);
    }

    private static string Normalize(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s.ToLowerInvariant())
            sb.Append(char.IsLetterOrDigit(ch) ? ch : ' ');

        var words = sb.ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => !Stopwords.Contains(w));

        return string.Join(' ', words);
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test --filter FuzzyTests`
Expected: PASS.

**Step 5: Commit**

```powershell
git add src/JevLauncher.Core/Fuzzy.cs tests/JevLauncher.Tests/FuzzyTests.cs
git commit -m "feat(core): add fuzzy scorer with tests"
```

---

## Task 4: Prefilter and candidate synthesis

**Files:**
- Create: `src/JevLauncher.Core/Prefilter.cs`
- Test: `tests/JevLauncher.Tests/PrefilterTests.cs`

**Step 1: Write the failing tests**

```csharp
using JevLauncher.Core;
using Xunit;

public class PrefilterTests
{
    private static Candidate C(string id, string title, string kw = "", CandidateKind kind = CandidateKind.OpenApp)
        => new(id, kind, title, "", kw, id);

    [Fact]
    public void Returns_at_most_13_scored_matches()
    {
        var all = Enumerable.Range(0, 50).Select(i => C($"c{i}", $"app {i}")).ToList();
        var result = Prefilter.TopMatches("app", all, 13);
        Assert.True(result.Count <= 13);
        Assert.All(result, c => Assert.True(c.Fuzzy > 0));
    }

    [Fact]
    public void Adds_calculator_candidate_for_math()
    {
        var all = new List<Candidate> { C("c0", "Calculator") };
        var result = Prefilter.BuildCandidates("15% of 240", all, 15);
        Assert.Contains(result, c => c.Kind == CandidateKind.Calculate && c.Title.Contains("36"));
    }

    [Fact]
    public void Adds_web_search_for_non_empty_query()
    {
        var all = new List<Candidate> { C("c0", "Calculator") };
        var result = Prefilter.BuildCandidates("dark", all, 15);
        Assert.Contains(result, c => c.Kind == CandidateKind.WebSearch);
    }

    [Fact]
    public void Caps_total_at_fifteen()
    {
        var all = Enumerable.Range(0, 50).Select(i => C($"c{i}", $"app {i}")).ToList();
        var result = Prefilter.BuildCandidates("app", all, 15);
        Assert.True(result.Count <= 15);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test --filter PrefilterTests`
Expected: FAIL.

**Step 3: Write the prefilter**

```csharp
using System.Globalization;

namespace JevLauncher.Core;

public static class Prefilter
{
    public static IReadOnlyList<Candidate> TopMatches(string query, IEnumerable<Candidate> all, int n)
    {
        return all
            .Select(c => c with { Fuzzy = Fuzzy.Score(query, c.Title, c.Keywords) })
            .Where(c => c.Fuzzy > 0)
            .OrderByDescending(c => c.Fuzzy)
            .Take(n)
            .ToList();
    }

    public static IReadOnlyList<Candidate> BuildCandidates(string query, IEnumerable<Candidate> all, int cap)
    {
        var list = TopMatches(query, all, Math.Max(0, cap - 2)).ToList();

        if (Calculator.TryParse(query, out var value))
        {
            var text = value.ToString("0.######", CultureInfo.InvariantCulture);
            list.Add(new Candidate("calc", CandidateKind.Calculate, $"= {text}",
                "Press Enter to copy", "calculator calc math", text));
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            list.Add(new Candidate("web", CandidateKind.WebSearch,
                $"Search the web for “{query}”", "Opens your default browser",
                "web search google bing", query));
        }

        return list.Take(cap).ToList();
    }

    public static string AssignIds(IReadOnlyList<Candidate> candidates)
    {
        var map = candidates
            .Select((c, i) => (Id: $"c{i}", c.Target, c.Title))
            .ToDictionary(x => x.Id, x => x.Title);
        return string.Join(", ", map.Keys);
    }
}
```

> `AssignIds` is intentionally simple; the App layer keeps its own id→candidate
> map. Adjust in Task 8 if needed.

**Step 4: Run tests to verify they pass**

Run: `dotnet test --filter PrefilterTests`
Expected: PASS.

**Step 5: Commit**

```powershell
git add src/JevLauncher.Core/Prefilter.cs tests/JevLauncher.Tests/PrefilterTests.cs
git commit -m "feat(core): add prefilter and candidate synthesis"
```

---

## Task 5: Jev request construction

**Files:**
- Create: `src/JevLauncher.Core/JevQuestions.cs`
- Test: `tests/JevLauncher.Tests/JevQuestionsTests.cs`

**Step 1: Write the failing tests**

```csharp
using System.Text.Json;
using JevLauncher.Core;
using Xunit;

public class JevQuestionsTests
{
    private static Conversation Sample() => new(
        new List<Candidate>
        {
            new("c0", CandidateKind.SystemToggle, "Toggle Dark Mode", "System toggle", "dark appearance theme", "dark-mode"),
            new("c1", CandidateKind.OpenFile, "Q3-Roadmap-Review.pdf", "PDF in ~/Downloads · modified 16 min ago", "pdf", @"C:\q3.pdf"),
            new("c2", CandidateKind.WebSearch, "Search the web for “the pdf I”", "Opens your default browser", "web", "the pdf I"),
        },
        new LauncherContext("Finder", new[] { "Finder", "Safari" }, "text", "afternoon", "Thursday"));

    [Fact]
    public void Builds_three_questions_with_ids()
    {
        var json = JevQuestions.BuildJson("the pdf I", Sample());
        using var doc = JsonDocument.Parse(json);
        var questions = doc.RootElement.GetProperty("questions");
        Assert.Equal("choice", questions.GetProperty("target").GetProperty("type").GetString());
        Assert.Equal("choice", questions.GetProperty("action").GetProperty("type").GetString());
        Assert.Equal("noul", questions.GetProperty("ready").GetProperty("type").GetString());

        var criteria = questions.GetProperty("target").GetProperty("criteria");
        Assert.True(criteria.TryGetProperty("c0", out _));
        Assert.True(criteria.TryGetProperty("c1", out _));
        Assert.True(criteria.TryGetProperty("none", out _));
    }

    [Fact]
    public void State_includes_query_and_context()
    {
        var json = JevQuestions.BuildJson("dark", Sample());
        using var doc = JsonDocument.Parse(json);
        var state = doc.RootElement.GetProperty("state");
        Assert.Equal("dark", state.GetProperty("query").GetString());
        Assert.Equal("Finder", state.GetProperty("context").GetProperty("frontmost_app").GetString());
        Assert.Equal("jev-latest", doc.RootElement.GetProperty("model").GetString());
    }

    [Fact]
    public void Candidate_ids_map_back_to_source()
    {
        var conv = Sample();
        var json = JevQuestions.BuildJson("pdf", conv);
        Assert.Equal("c0", conv.Candidates[0].Id);
        Assert.Contains("c0", json);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test --filter JevQuestionsTests`
Expected: FAIL.

**Step 3: Write request construction**

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;

namespace JevLauncher.Core;

public static class JevQuestions
{
    public const string QueryNote =
        "Text the user has typed so far into a Spotlight-style Windows launcher. " +
        "It is often an incomplete prefix or a short natural-language phrase.";

    public static string BuildJson(string query, Conversation conv)
    {
        var candidates = new JsonArray();
        var targetCriteria = new JsonObject();

        for (var i = 0; i < conv.Candidates.Count; i++)
        {
            var c = conv.Candidates[i];
            candidates.Add(new JsonObject
            {
                ["id"] = c.Id,
                ["kind"] = KindName(c.Kind),
                ["title"] = c.Title,
                ["detail"] = c.Detail,
            });
            targetCriteria[c.Id] = $"{c.Title} — {c.Detail}";
        }
        targetCriteria["none"] = "No candidate is the intended target.";

        var context = new JsonObject
        {
            ["frontmost_app"] = conv.Context.FrontmostApp,
            ["recent_apps"] = new JsonArray(conv.Context.RecentApps.Select(a => (JsonNode)a!).ToArray()),
            ["clipboard_kind"] = conv.Context.ClipboardKind,
            ["time_of_day"] = conv.Context.TimeOfDay,
            ["weekday"] = conv.Context.Weekday,
        };

        var state = new JsonObject
        {
            ["query"] = query,
            ["query_note"] = QueryNote,
            ["context"] = context,
            ["candidates"] = candidates,
        };

        var questions = new JsonObject
        {
            ["target"] = new JsonObject
            {
                ["type"] = "choice",
                ["instructions"] =
                    "Which entry in candidates is the item they intend to open or run? " +
                    "Treat query as a possibly incomplete prefix or paraphrase. Match on meaning. " +
                    "detail carries recency: prefer the entry whose detail says it was modified most recently. " +
                    "candidates is the complete option set; use none only if nothing fits.",
                ["criteria"] = targetCriteria,
            },
            ["action"] = new JsonObject
            {
                ["type"] = "choice",
                ["instructions"] = "What action does query intend?",
                ["criteria"] = new JsonObject
                {
                    ["open_app"] = "Launch an installed application.",
                    ["open_file"] = "Open a local file or folder.",
                    ["web_search"] = "Search the web; only a fallback when nothing local fits.",
                    ["calculate"] = "Evaluate arithmetic.",
                    ["system_toggle"] = "Flip a system setting (dark mode, wifi, sleep, mute…).",
                    ["run_shortcut"] = "Run a named shortcut or script.",
                    ["unclear"] = "The intent is not clear.",
                },
            },
            ["ready"] = new JsonObject
            {
                ["type"] = "noul",
                ["instructions"] =
                    "The launcher is about to run the best-matching candidate the instant the user presses Enter. " +
                    "candidates is the complete option set and web_search is only a fallback. " +
                    "A short prefix can already be unambiguous (for example 'dark'). " +
                    "Is query already unambiguous enough to run the top candidate?",
            },
        };

        var root = new JsonObject
        {
            ["model"] = "jev-latest",
            ["state"] = state,
            ["questions"] = questions,
        };

        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    public static string KindName(CandidateKind kind) => kind switch
    {
        CandidateKind.OpenApp => "open_app",
        CandidateKind.OpenFile => "open_file",
        CandidateKind.WebSearch => "web_search",
        CandidateKind.Calculate => "calculate",
        CandidateKind.SystemToggle => "system_toggle",
        CandidateKind.RunShortcut => "run_shortcut",
        _ => "unclear",
    };
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test --filter JevQuestionsTests`
Expected: PASS.

**Step 5: Commit**

```powershell
git add src/JevLauncher.Core/JevQuestions.cs tests/JevLauncher.Tests/JevQuestionsTests.cs
git commit -m "feat(core): build Jev request payload"
```

---

## Task 6: Jev response parsing

**Files:**
- Create: `src/JevLauncher.Core/JevResponse.cs`
- Test: `tests/JevLauncher.Tests/JevResponseTests.cs`

**Step 1: Write the failing tests**

```csharp
using JevLauncher.Core;
using Xunit;

public class JevResponseTests
{
    private const string Json = """
    {
      "answers": {
        "target": { "type": "choice", "choice": "c1", "probabilities": { "c0": 0.02, "c1": 0.98, "none": 0.0 }, "confidence": 0.8 },
        "action": { "type": "choice", "choice": "open_file", "probabilities": { "open_file": 0.9, "web_search": 0.1 }, "confidence": 0.7 },
        "ready": { "type": "noul", "noul": 0.4 }
      },
      "usage": { "input_tokens": 1400, "output_tokens": 0 }
    }
    """;

    [Fact]
    public void Parses_target_distribution()
    {
        var r = JevResponse.Parse(Json);
        Assert.Equal("c1", r.TargetChoice);
        Assert.Equal(0.98, r.TargetProbabilities["c1"], 3);
        Assert.Equal(0.4, r.Ready, 3);
        Assert.Equal("open_file", r.ActionChoice);
        Assert.Equal(1400, r.InputTokens);
    }

    [Fact]
    public void Missing_answers_yields_safe_defaults()
    {
        var r = JevResponse.Parse("{}");
        Assert.Equal("none", r.TargetChoice);
        Assert.Empty(r.TargetProbabilities);
        Assert.Equal(0, r.Ready);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test --filter JevResponseTests`
Expected: FAIL.

**Step 3: Write the parser**

```csharp
using System.Text.Json;

namespace JevLauncher.Core;

public sealed record JevResponse(
    string TargetChoice,
    IReadOnlyDictionary<string, double> TargetProbabilities,
    string ActionChoice,
    double Ready,
    int InputTokens,
    int OutputTokens)
{
    public static JevResponse Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var targetChoice = "none";
        var probs = new Dictionary<string, double>();
        var actionChoice = "unclear";
        double ready = 0;
        int input = 0, output = 0;

        if (root.TryGetProperty("answers", out var answers))
        {
            if (answers.TryGetProperty("target", out var target))
            {
                if (target.TryGetProperty("choice", out var ch) && ch.ValueKind == JsonValueKind.String)
                    targetChoice = ch.GetString()!;
                if (target.TryGetProperty("probabilities", out var p) && p.ValueKind == JsonValueKind.Object)
                    foreach (var prop in p.EnumerateObject())
                        probs[prop.Name] = prop.Value.GetDouble();
            }
            if (answers.TryGetProperty("action", out var action) &&
                action.TryGetProperty("choice", out var ach) && ach.ValueKind == JsonValueKind.String)
                actionChoice = ach.GetString()!;

            if (answers.TryGetProperty("ready", out var r) &&
                r.TryGetProperty("noul", out var n) && n.ValueKind == JsonValueKind.Number)
                ready = n.GetDouble();
        }

        if (root.TryGetProperty("usage", out var usage))
        {
            if (usage.TryGetProperty("input_tokens", out var it) && it.ValueKind == JsonValueKind.Number)
                input = it.GetInt32();
            if (usage.TryGetProperty("output_tokens", out var ot) && ot.ValueKind == JsonValueKind.Number)
                output = ot.GetInt32();
        }

        return new JevResponse(targetChoice, probs, actionChoice, ready, input, output);
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test --filter JevResponseTests`
Expected: PASS.

**Step 5: Commit**

```powershell
git add src/JevLauncher.Core/JevResponse.cs tests/JevLauncher.Tests/JevResponseTests.cs
git commit -m "feat(core): parse Jev responses"
```

---

## Task 7: Ranker

**Files:**
- Create: `src/JevLauncher.Core/Ranker.cs`
- Test: `tests/JevLauncher.Tests/RankerTests.cs`

**Step 1: Write the failing tests**

```csharp
using JevLauncher.Core;
using Xunit;

public class RankerTests
{
    private static Candidate C(string id, CandidateKind kind, string title, double fuzzy)
        => new(id, kind, title, "", "", null, fuzzy);

    [Fact]
    public void Without_jev_answer_falls_back_to_fuzzy_order()
    {
        var list = new[] { C("c0", CandidateKind.OpenApp, "a", 10), C("c1", CandidateKind.OpenApp, "b", 50) };
        var ranked = Ranker.Rank(list, null);
        Assert.Equal("c1", ranked[0].Candidate.Id);
    }

    [Fact]
    public void Jev_target_dominates_the_score()
    {
        var list = new[] { C("c0", CandidateKind.OpenFile, "old", 90), C("c1", CandidateKind.OpenFile, "new", 10) };
        var resp = new JevResponse("c1", new Dictionary<string, double> { ["c0"] = 0.02, ["c1"] = 0.98 },
            "open_file", 0.4, 1400, 0);
        var ranked = Ranker.Rank(list, resp);
        Assert.Equal("c1", ranked[0].Candidate.Id);
    }

    [Fact]
    public void Action_mismatch_loses_the_boost()
    {
        var list = new[] { C("c0", CandidateKind.WebSearch, "web", 80), C("c1", CandidateKind.OpenApp, "app", 30) };
        var resp = new JevResponse("c1", new Dictionary<string, double> { ["c1"] = 0.6, ["c0"] = 0.4 },
            "open_app", 0.4, 1400, 0);
        var ranked = Ranker.Rank(list, resp);
        Assert.Equal("c1", ranked[0].Candidate.Id);
    }

    [Fact]
    public void Ready_badge_when_ready_high_or_target_certain()
    {
        var list = new[] { C("c0", CandidateKind.OpenApp, "app", 30) };
        var confident = new JevResponse("c0", new Dictionary<string, double> { ["c0"] = 0.95 }, "open_app", 0.1, 1, 0);
        var ranked = Ranker.Rank(list, confident);
        Assert.True(ranked[0].IsTopReady);

        var unsure = new JevResponse("c0", new Dictionary<string, double> { ["c0"] = 0.5 }, "open_app", 0.3, 1, 0);
        Assert.False(Ranker.Rank(list, unsure)[0].IsTopReady);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test --filter RankerTests`
Expected: FAIL.

**Step 3: Write the ranker**

```csharp
namespace JevLauncher.Core;

public static class Ranker
{
    private const double TargetWeight = 0.65;
    private const double ActionWeight = 0.20;
    private const double FuzzyWeight = 0.15;

    public static IReadOnlyList<ScoredCandidate> Rank(IReadOnlyList<Candidate> candidates, JevResponse? response)
    {
        var maxFuzzy = candidates.Count == 0 ? 1 : Math.Max(1, candidates.Max(c => c.Fuzzy));

        var scored = candidates.Select(c =>
        {
            var pTarget = response is not null && response.TargetProbabilities.TryGetValue(c.Id, out var p) ? p : 0;
            var actionMatch = response is not null &&
                              string.Equals(JevQuestions.KindName(c.Kind), response.ActionChoice, StringComparison.Ordinal)
                ? 1.0 : 0.0;
            var fuzzyNorm = c.Fuzzy / maxFuzzy;

            var score = response is null
                ? fuzzyNorm
                : TargetWeight * pTarget + ActionWeight * actionMatch + FuzzyWeight * fuzzyNorm;

            return new ScoredCandidate(c, score, pTarget, false);
        })
        .OrderByDescending(s => s.Score)
        .ToList();

        if (scored.Count > 0 && response is not null)
        {
            var top = scored[0];
            var ready = response.Ready >= 0.6 || top.TargetProbability >= 0.9;
            scored[0] = top with { IsTopReady = ready };
        }

        return scored;
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test --filter RankerTests`
Expected: PASS.

**Step 5: Commit**

```powershell
git add src/JevLauncher.Core/Ranker.cs tests/JevLauncher.Tests/RankerTests.cs
git commit -m "feat(core): rank candidates with Jev signals"
```

---

## Task 8: Stats (latency and cost)

**Files:**
- Create: `src/JevLauncher.Core/Stats.cs`
- Test: `tests/JevLauncher.Tests/StatsTests.cs`

**Step 1: Write the failing tests**

```csharp
using JevLauncher.Core;
using Xunit;

public class StatsTests
{
    [Fact]
    public void Tracks_last_p50_p95_and_cost()
    {
        var s = new Stats();
        s.Record(100, 1400);
        s.Record(200, 1400);
        s.Record(300, 1400);
        Assert.Equal(300, s.LastMs);
        Assert.Equal(200, s.P50);
        Assert.Equal(300, s.P95);
        Assert.Equal(3, s.Decisions);
        Assert.Equal(4200, s.InputTokens);
        Assert.Equal(4200 / 1_000_000.0 * 0.042, s.EstimatedCost, 6);
    }

    [Fact]
    public void Empty_stats_are_zero()
    {
        var s = new Stats();
        Assert.Equal(0, s.LastMs);
        Assert.Equal(0, s.P50);
        Assert.Equal(0, s.Decisions);
        Assert.Equal(0, s.EstimatedCost);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test --filter StatsTests`
Expected: FAIL.

**Step 3: Write Stats**

```csharp
namespace JevLauncher.Core;

public sealed class Stats
{
    public const double DollarsPerMillionInputTokens = 0.042;

    private readonly List<double> _latencies = new();
    private long _inputTokens;

    public double LastMs { get; private set; }
    public int Decisions { get; private set; }
    public long InputTokens => _inputTokens;

    public double P50 => Percentile(0.50);
    public double P95 => Percentile(0.95);
    public double EstimatedCost => _inputTokens / 1_000_000.0 * DollarsPerMillionInputTokens;

    public void Record(double milliseconds, int inputTokens)
    {
        LastMs = milliseconds;
        Decisions++;
        _inputTokens += inputTokens;
        _latencies.Add(milliseconds);
    }

    private double Percentile(double p)
    {
        if (_latencies.Count == 0) return 0;
        var sorted = _latencies.OrderBy(x => x).ToList();
        var idx = (int)Math.Ceiling(p * sorted.Count) - 1;
        return sorted[Math.Clamp(idx, 0, sorted.Count - 1)];
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test --filter StatsTests`
Expected: PASS.

**Step 5: Commit**

```powershell
git add src/JevLauncher.Core/Stats.cs tests/JevLauncher.Tests/StatsTests.cs
git commit -m "feat(core): track latency and cost stats"
```

---

## Task 9: Jev HTTP client with stale handling

**Files:**
- Create: `src/JevLauncher.Core/JevClient.cs`
- Test: `tests/JevLauncher.Tests/JevClientTests.cs`

**Step 1: Write the failing tests (no network: fake handler)**

```csharp
using System.Net;
using System.Text;
using JevLauncher.Core;
using Xunit;

public class JevClientTests
{
    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _f;
        public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> f) => _f = f;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(_f(request));
    }

    private const string OkBody = """{ "answers": { "target": { "choice": "c0", "probabilities": { "c0": 1.0 } }, "action": { "choice": "open_app" }, "ready": { "noul": 0.9 } }, "usage": { "input_tokens": 100, "output_tokens": 0 } }""";

    [Fact]
    public async Task Sends_bearer_token_and_returns_response()
    {
        HttpRequestMessage? seen = null;
        var handler = new FakeHandler(req =>
        {
            seen = req;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(OkBody, Encoding.UTF8, "application/json") };
        });
        var client = new JevClient(new HttpClient(handler), "secret");

        var result = await client.QueryAsync("dark", new Conversation(
            new[] { new Candidate("c0", CandidateKind.OpenApp, "Dark", "", "", null) },
            new LauncherContext("Finder", Array.Empty<string>(), "text", "afternoon", "Thursday")));

        Assert.NotNull(result);
        Assert.Equal("Bearer", seen!.Headers.Authorization!.Scheme);
        Assert.Equal("secret", seen.Headers.Authorization.Parameter);
        Assert.Equal("https://api.typesafe.ai/v1/systemone", seen.RequestUri!.ToString());
    }

    [Fact]
    public async Task Returns_null_on_error_status()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var client = new JevClient(new HttpClient(handler), "bad");
        var result = await client.QueryAsync("dark", new Conversation(
            Array.Empty<Candidate>(),
            new LauncherContext("Finder", Array.Empty<string>(), "text", "afternoon", "Thursday")));
        Assert.Null(result);
    }

    [Fact]
    public async Task Returns_null_when_no_key()
    {
        var client = new JevClient(new HttpClient(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))), null);
        var result = await client.QueryAsync("dark", new Conversation(
            Array.Empty<Candidate>(),
            new LauncherContext("Finder", Array.Empty<string>(), "text", "afternoon", "Thursday")));
        Assert.Null(result);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test --filter JevClientTests`
Expected: FAIL.

**Step 3: Write the client**

```csharp
using System.Net.Http.Headers;
using System.Text;

namespace JevLauncher.Core;

public sealed class JevClient
{
    private const string Endpoint = "https://api.typesafe.ai/v1/systemone";

    private readonly HttpClient _http;
    private readonly string? _apiKey;

    public JevClient(HttpClient http, string? apiKey)
    {
        _http = http;
        _apiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey;
        _http.Timeout = TimeSpan.FromSeconds(5);
    }

    public bool HasKey => _apiKey is not null;

    public async Task<JevResponse?> QueryAsync(string query, Conversation conversation, CancellationToken ct = default)
    {
        if (_apiKey is null) return null;

        try
        {
            var json = JevQuestions.BuildJson(query, conversation);
            using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);
            if (!response.IsSuccessStatusCode) return null;

            var body = await response.Content.ReadAsStringAsync(ct);
            return JevResponse.Parse(body);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return null;
        }
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test --filter JevClientTests`
Expected: PASS.

**Step 5: Commit**

```powershell
git add src/JevLauncher.Core/JevClient.cs tests/JevLauncher.Tests/JevClientTests.cs
git commit -m "feat(core): add Jev HTTP client"
```

---

## Task 10: Executor — launch and calculate

**Files:**
- Create: `src/JevLauncher.Core/Executor.cs`
- Test: `tests/JevLauncher.Tests/ExecutorTests.cs`

**Step 1: Write the failing tests**

Test the pure parts only (command/URL building and clipboard text), not the OS
launch, so tests are hermetic.

```csharp
using JevLauncher.Core;
using Xunit;

public class ExecutorTests
{
    [Fact]
    public void Web_search_builds_default_browser_url()
    {
        var url = Executor.BuildSearchUrl("hello world", "https://www.google.com/search?q={0}");
        Assert.Equal("https://www.google.com/search?q=hello%20world", url);
    }

    [Fact]
    public void Calculate_copies_the_numeric_result()
    {
        var c = new Candidate("calc", CandidateKind.Calculate, "= 36", "", "", "36");
        Assert.Equal("36", Executor.ClipboardTextFor(c));
    }

    [Fact]
    public void Non_calculate_has_no_clipboard_text()
    {
        var c = new Candidate("c0", CandidateKind.OpenApp, "app", "", "", null);
        Assert.Null(Executor.ClipboardTextFor(c));
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test --filter ExecutorTests`
Expected: FAIL.

**Step 3: Write the executor (launching parts)**

```csharp
using System.Diagnostics;
using System.Text;

namespace JevLauncher.Core;

public static class Executor
{
    public static string DefaultSearchTemplate => "https://www.google.com/search?q={0}";

    public static string BuildSearchUrl(string query, string template)
        => string.Format(template, Uri.EscapeDataString(query));

    public static string? ClipboardTextFor(Candidate candidate)
        => candidate.Kind == CandidateKind.Calculate ? candidate.Target : null;

    public static void Launch(Candidate candidate, Action<string> setClipboard)
    {
        switch (candidate.Kind)
        {
            case CandidateKind.Calculate:
                if (candidate.Target is { } text) setClipboard(text);
                break;
            case CandidateKind.WebSearch:
                ShellExecute(BuildSearchUrl(candidate.Target ?? string.Empty, DefaultSearchTemplate));
                break;
            case CandidateKind.OpenApp:
            case CandidateKind.OpenFile:
            case CandidateKind.RunShortcut:
                if (!string.IsNullOrWhiteSpace(candidate.Target)) ShellExecute(candidate.Target);
                break;
        }
    }

    private static void ShellExecute(string target)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = target,
            UseShellExecute = true,
        });
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test --filter ExecutorTests`
Expected: PASS.

**Step 5: Commit**

```powershell
git add src/JevLauncher.Core/Executor.cs tests/JevLauncher.Tests/ExecutorTests.cs
git commit -m "feat(core): add executor for launch and calculate"
```

---

## Task 11: System toggles

**Files:**
- Create: `src/JevLauncher.Core/SystemToggles.cs`
- Create: `src/JevLauncher.Core/Native/NativeMethods.cs`
- Test: `tests/JevLauncher.Tests/SystemTogglesTests.cs`

**Step 1: Write the failing tests**

Test only parsing/registry-name logic, not the actual OS calls.

```csharp
using JevLauncher.Core;
using Xunit;

public class SystemTogglesTests
{
    [Fact]
    public void Provides_the_seven_required_toggles()
    {
        var ids = SystemToggles.All.Select(t => t.Id).ToHashSet();
        Assert.Superset(new HashSet<string>
        {
            "dark-mode", "wifi", "sleep", "lock", "empty-trash", "hidden-files", "mute",
        }, ids);
    }

    [Fact]
    public void Toggle_candidates_are_system_toggles()
    {
        Assert.All(SystemToggles.BuildCandidates(), c => Assert.Equal(CandidateKind.SystemToggle, c.Kind));
    }

    [Fact]
    public void Parses_wifi_interface_name_from_netsh_output()
    {
        const string output = """
        Name                   : Wi-Fi
        Description            : Intel(R) Wi-Fi 6
        State                  : connected
        """;
        Assert.Equal("Wi-Fi", SystemToggles.ParseWifiInterface(output));
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test --filter SystemTogglesTests`
Expected: FAIL.

**Step 3: Write native methods**

```csharp
using System.Runtime.InteropServices;

namespace JevLauncher.Core.Native;

internal static class NativeMethods
{
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool LockWorkStation();

    [DllImport("powrprof.dll", SetLastError = true)]
    internal static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    internal static extern int SHEmptyRecycleBin(IntPtr hwnd, string? rootPath, uint flags);

    [DllImport("shell32.dll")]
    internal static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    internal static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, string lParam,
        uint flags, uint timeout, out IntPtr result);

    internal const int SHCNE_ASSOCCHANGED = 0x08000000;
    internal const uint SHCNF_IDLIST = 0x0000;
    internal const uint WM_SETTINGCHANGE = 0x001A;
    internal const uint SMTO_ABORTIFHUNG = 0x0002;
    internal const uint HWND_BROADCAST = 0xFFFF;
}
```

**Step 4: Write the toggles**

```csharp
using System.Diagnostics;
using System.Text.RegularExpressions;
using JevLauncher.Core.Native;
using Microsoft.Win32;

namespace JevLauncher.Core;

public sealed record SystemToggle(string Id, string Title, string Detail, string Keywords);

public static class SystemToggles
{
    public static readonly IReadOnlyList<SystemToggle> All = new[]
    {
        new SystemToggle("dark-mode", "Toggle Dark Mode", "System toggle", "dark light mode appearance theme"),
        new SystemToggle("wifi", "Toggle Wi-Fi", "System toggle", "wifi wireless network internet"),
        new SystemToggle("sleep", "Sleep", "System toggle", "sleep suspend power"),
        new SystemToggle("lock", "Lock Screen", "System toggle", "lock screen secure"),
        new SystemToggle("empty-trash", "Empty Recycle Bin", "System toggle", "empty trash recycle bin delete"),
        new SystemToggle("hidden-files", "Toggle Hidden Files", "System toggle", "hidden files show hide explorer"),
        new SystemToggle("mute", "Toggle Mute", "System toggle", "mute volume sound audio"),
    };

    public static IReadOnlyList<Candidate> BuildCandidates() =>
        All.Select(t => new Candidate(t.Id, CandidateKind.SystemToggle, t.Title, t.Detail, t.Keywords, t.Id)).ToList();

    public static string? ParseWifiInterface(string netshOutput)
    {
        var match = Regex.Match(netshOutput, @"^\s*Name\s*:\s*(.+?)\s*$", RegexOptions.Multiline);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    public static void Execute(string id, Action<string> reportError)
    {
        switch (id)
        {
            case "dark-mode": ToggleDarkMode(); break;
            case "wifi": ToggleWifi(reportError); break;
            case "sleep": NativeMethods.SetSuspendState(false, false, false); break;
            case "lock": NativeMethods.LockWorkStation(); break;
            case "empty-trash": NativeMethods.SHEmptyRecycleBin(IntPtr.Zero, null, 0); break;
            case "hidden-files": ToggleHiddenFiles(); break;
            case "mute": AudioMute.Toggle(); break;
        }
    }

    private static void ToggleDarkMode()
    {
        const string key = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        using var k = Registry.CurrentUser.OpenSubKey(key, writable: true)!;
        var current = k.GetValue("AppsUseLightTheme") as int? ?? 1;
        var next = current == 0 ? 1 : 0;
        k.SetValue("AppsUseLightTheme", next, RegistryValueKind.DWord);
        k.SetValue("SystemUsesLightTheme", next, RegistryValueKind.DWord);
        NativeMethods.SendMessageTimeout((IntPtr)NativeMethods.HWND_BROADCAST, NativeMethods.WM_SETTINGCHANGE,
            IntPtr.Zero, "ImmersiveColorSet", NativeMethods.SMTO_ABORTIFHUNG, 1000, out _);
    }

    private static void ToggleHiddenFiles()
    {
        const string key = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
        using var k = Registry.CurrentUser.OpenSubKey(key, writable: true)!;
        var current = k.GetValue("Hidden") as int? ?? 2;
        k.SetValue("Hidden", current == 1 ? 2 : 1, RegistryValueKind.DWord);
        NativeMethods.SHChangeNotify(NativeMethods.SHCNE_ASSOCCHANGED, NativeMethods.SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
    }

    private static void ToggleWifi(Action<string> reportError)
    {
        var psi = new ProcessStartInfo("netsh", "interface show interface")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var proc = Process.Start(psi);
        if (proc is null) { reportError("Could not query network interfaces."); return; }
        var output = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit();

        var name = ParseWifiInterface(output);
        if (name is null) { reportError("No Wi-Fi interface found."); return; }

        var state = new ProcessStartInfo("netsh", $"interface show interface name=\"{name}\"")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var stateProc = Process.Start(state)!;
        var stateOut = stateProc.StandardOutput.ReadToEnd();
        stateProc.WaitForExit();
        var isEnabled = stateOut.Contains("Enabled", StringComparison.OrdinalIgnoreCase);

        var action = isEnabled ? "admin=disabled" : "admin=enabled";
        var run = new ProcessStartInfo("netsh", $"interface set interface name=\"{name}\" {action}")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var runProc = Process.Start(run);
        runProc?.WaitForExit();
        if (runProc is null || runProc.ExitCode != 0)
            reportError("Wi-Fi toggle needs administrator rights.");
    }
}
```

**Step 5: Write the audio mute helper**

```csharp
using System.Runtime.InteropServices;

namespace JevLauncher.Core.Native;

internal static class AudioMute
{
    public static void Toggle()
    {
        var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
        try
        {
            enumerator.GetDefaultAudioEndpoint(0, 0, out var device);
            var iid = typeof(IAudioEndpointVolume).GUID;
            device.Activate(ref iid, 0, IntPtr.Zero, out var volumeObj);
            var volume = (IAudioEndpointVolume)volumeObj;
            volume.GetMute(out var muted);
            volume.SetMute(!muted, Guid.Empty);
        }
        finally
        {
            Marshal.ReleaseComObject(enumerator);
        }
    }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorComObject { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        int RegisterControlChangeNotify(IntPtr pNotify);
        int UnregisterControlChangeNotify(IntPtr pNotify);
        int GetChannelCount(out int count);
        int SetMasterVolumeLevel(float level, Guid eventContext);
        int SetMasterVolumeLevelScalar(float level, Guid eventContext);
        int GetMasterVolumeLevel(out float level);
        int GetMasterVolumeLevelScalar(out float level);
        int SetChannelVolumeLevel(uint channel, float level, Guid eventContext);
        int SetChannelVolumeLevelScalar(uint channel, float level, Guid eventContext);
        int GetChannelVolumeLevel(uint channel, out float level);
        int GetChannelVolumeLevelScalar(uint channel, out float level);
        int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, Guid eventContext);
        int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }
}
```

**Step 6: Run tests to verify they pass**

Run: `dotnet test --filter SystemTogglesTests`
Expected: PASS.

**Step 7: Commit**

```powershell
git add src/JevLauncher.Core/SystemToggles.cs src/JevLauncher.Core/Native tests/JevLauncher.Tests/SystemTogglesTests.cs
git commit -m "feat(core): add Windows system toggles"
```

---

## Task 12: Local index — apps and files

**Files:**
- Create: `src/JevLauncher.Core/LocalIndex.cs`
- Test: `tests/JevLauncher.Tests/LocalIndexTests.cs`

**Step 1: Write the failing tests (pure helpers only)**

```csharp
using JevLauncher.Core;
using Xunit;

public class LocalIndexTests
{
    [Fact]
    public void Start_menu_shortcut_becomes_open_app_candidate()
    {
        var c = LocalIndex.AppCandidateFromShortcut("Google Chrome.lnk", @"C:\...\Google Chrome.lnk");
        Assert.Equal(CandidateKind.OpenApp, c.Kind);
        Assert.Equal("Google Chrome", c.Title);
    }

    [Fact]
    public void File_candidate_includes_recency_detail()
    {
        var age = DateTime.Now.AddMinutes(-16);
        var c = LocalIndex.FileCandidateFromPath(@"C:\Users\me\Downloads\Q3-Roadmap-Review.pdf", age);
        Assert.Contains("modified", c.Detail);
        Assert.Contains("PDF", c.Detail);
        Assert.Equal(CandidateKind.OpenFile, c.Kind);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test --filter LocalIndexTests`
Expected: FAIL.

**Step 3: Write the index builder**

```csharp
using System.IO;

namespace JevLauncher.Core;

public static class LocalIndex
{
    private static readonly string[] IndexedFolders =
    {
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + @"\Downloads",
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
    };

    public static IReadOnlyList<Candidate> Build()
    {
        var list = new List<Candidate>();
        list.AddRange(SystemToggles.BuildCandidates());
        list.AddRange(StartMenuApps());
        list.AddRange(Files());
        return list;
    }

    public static IEnumerable<Candidate> StartMenuApps()
    {
        var roots = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs"),
        };

        foreach (var root in roots.Where(Directory.Exists))
        {
            foreach (var lnk in Directory.EnumerateFiles(root, "*.lnk", SearchOption.AllDirectories))
                yield return AppCandidateFromShortcut(Path.GetFileName(lnk), lnk);
        }
    }

    public static Candidate AppCandidateFromShortcut(string fileName, string path)
    {
        var title = Path.GetFileNameWithoutExtension(fileName);
        return new Candidate($"app:{path}", CandidateKind.OpenApp, title, "Application", title, path);
    }

    public static IEnumerable<Candidate> Files()
    {
        foreach (var folder in IndexedFolders.Where(Directory.Exists))
        {
            foreach (var file in EnumerateFolder(folder, 0))
                yield return file;
        }
    }

    private static IEnumerable<Candidate> EnumerateFolder(string folder, int depth)
    {
        const int cap = 400;
        IEnumerable<string> files;
        IEnumerable<string> dirs;
        try
        {
            files = Directory.EnumerateFiles(folder).Take(cap).ToList();
            dirs = Directory.EnumerateDirectories(folder).Take(cap).ToList();
        }
        catch (UnauthorizedAccessException) { yield break; }

        foreach (var file in files)
        {
            var modified = File.GetLastWriteTime(file);
            yield return FileCandidateFromPath(file, modified);
        }

        if (depth >= 1) yield break;
        foreach (var dir in dirs)
            foreach (var nested in EnumerateFolder(dir, depth + 1))
                yield return nested;
    }

    public static Candidate FileCandidateFromPath(string path, DateTime modified)
    {
        var title = Path.GetFileName(path);
        var ext = Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
        var folder = Path.GetFileName(Path.GetDirectoryName(path)) ?? string.Empty;
        var detail = string.IsNullOrEmpty(ext)
            ? $"Folder in {folder} · modified {Recency.Describe(modified)}"
            : $"{ext} in {folder} · modified {Recency.Describe(modified)}";
        return new Candidate($"file:{path}", CandidateKind.OpenFile, title, detail, title, path);
    }
}

public static class Recency
{
    public static string Describe(DateTime modified)
    {
        var delta = DateTime.Now - modified;
        if (delta.TotalMinutes < 1) return "just now";
        if (delta.TotalMinutes < 60) return $"{(int)delta.TotalMinutes} min ago";
        if (delta.TotalHours < 24) return $"{(int)delta.TotalHours} h ago";
        if (delta.TotalDays < 30) return $"{(int)delta.TotalDays} d ago";
        return $"{Math.Max(1, (int)(delta.TotalDays / 30))} months ago";
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test --filter LocalIndexTests`
Expected: PASS.

**Step 5: Commit**

```powershell
git add src/JevLauncher.Core/LocalIndex.cs tests/JevLauncher.Tests/LocalIndexTests.cs
git commit -m "feat(core): index start menu apps and files"
```

---

## Task 13: Context provider (foreground app, clipboard kind)

**Files:**
- Create: `src/JevLauncher.Core/ContextProvider.cs`
- Test: `tests/JevLauncher.Tests/ContextProviderTests.cs`

**Step 1: Write the failing tests**

```csharp
using JevLauncher.Core;
using Xunit;

public class ContextProviderTests
{
    [Theory]
    [InlineData(14, "morning")]
    [InlineData(15, "afternoon")]
    [InlineData(20, "evening")]
    [InlineData(3, "night")]
    public void Classifies_time_of_day(int hour, string expected)
    {
        Assert.Equal(expected, ContextProvider.TimeOfDay(new DateTime(2026, 9, 18, hour, 0, 0)));
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test --filter ContextProviderTests`
Expected: FAIL.

**Step 3: Write the provider**

```csharp
using System.Diagnostics;
using System.Windows.Forms;
using JevLauncher.Core.Native;

namespace JevLauncher.Core;

public static class ContextProvider
{
    public static LauncherContext Capture(IReadOnlyList<string> recentApps)
    {
        var now = DateTime.Now;
        return new LauncherContext(
            FrontmostApp(),
            recentApps,
            ClipboardKind(),
            TimeOfDay(now),
            now.DayOfWeek.ToString());
    }

    public static string TimeOfDay(DateTime now) => now.Hour switch
    {
        >= 5 and < 12 => "morning",
        >= 12 and < 18 => "afternoon",
        >= 18 and < 22 => "evening",
        _ => "night",
    };

    private static string FrontmostApp()
    {
        try
        {
            var hwnd = NativeMethods.GetForegroundWindow();
            NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
            using var p = Process.GetProcessById((int)pid);
            return p.ProcessName;
        }
        catch
        {
            return "unknown";
        }
    }

    private static string ClipboardKind()
    {
        try
        {
            return Clipboard.ContainsImage() ? "image"
                : Clipboard.ContainsText() ? "text"
                : Clipboard.ContainsFileDropList() ? "files"
                : "empty";
        }
        catch
        {
            return "unknown";
        }
    }
}
```

> Note: referencing `System.Windows.Forms` requires `<UseWindowsForms>true</UseWindowsForms>`
> in Core's csproj. If you prefer to keep Core WinForms-free, move the
> clipboard check to the App project behind an `IClipboardKindProvider`
> interface. Recommended: add the interface, default implementation in App.

**Step 4: Run tests to verify they pass**

Run: `dotnet test --filter ContextProviderTests`
Expected: PASS.

**Step 5: Commit**

```powershell
git add src/JevLauncher.Core/ContextProvider.cs tests/JevLauncher.Tests/ContextProviderTests.cs
git commit -m "feat(core): capture launcher context"
```

---

## Task 14: Orchestrator (sequence numbers, stale handling)

**Files:**
- Create: `src/JevLauncher.Core/LauncherEngine.cs`
- Test: `tests/JevLauncher.Tests/LauncherEngineTests.cs`

**Step 1: Write the failing tests**

```csharp
using JevLauncher.Core;
using Xunit;

public class LauncherEngineTests
{
    private sealed class FakeJev : IJevQuery
    {
        public readonly List<string> Queries = new();
        public Func<string, JevResponse?> Respond = _ => null;

        public Task<JevResponse?> QueryAsync(string query, Conversation conversation, CancellationToken ct = default)
        {
            Queries.Add(query);
            return Task.FromResult(Respond(query));
        }
    }

    private static LauncherEngine NewEngine(FakeJev jev)
    {
        var index = new List<Candidate>
        {
            new("c0", CandidateKind.OpenApp, "Dark Mode", "toggle", "dark mode", "c0"),
            new("c1", CandidateKind.OpenFile, "notes.txt", "TXT", "notes", "c1"),
        };
        return new LauncherEngine(index, jev, new Stats(), static () =>
            new LauncherContext("Finder", Array.Empty<string>(), "text", "afternoon", "Thursday"));
    }

    [Fact]
    public void Empty_query_returns_nothing()
    {
        var engine = NewEngine(new FakeJev());
        Assert.Empty(engine.Update(""));
    }

    [Fact]
    public void Fuzzy_order_when_jev_unavailable()
    {
        var engine = NewEngine(new FakeJev { Respond = _ => null });
        var rows = engine.Update("notes");
        Assert.Equal("notes.txt", rows[0].Candidate.Title);
    }

    [Fact]
    public async Task Stale_response_is_discarded()
    {
        var jev = new FakeJev();
        var engine = NewEngine(jev);
        var slow = new TaskCompletionSource<JevResponse?>();
        jev.Respond = q => q == "notes" ? null : null;

        engine.Update("n");
        var t1 = engine.UpdateAsync("notes");
        engine.Update("notesx");
        var t2 = engine.UpdateAsync("notesx");
        await Task.WhenAll(t1, t2);

        var rows = engine.Update("notes");
        Assert.Equal("notes.txt", rows[0].Candidate.Title);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test --filter LauncherEngineTests`
Expected: FAIL.

**Step 3: Write the engine**

```csharp
namespace JevLauncher.Core;

public interface IJevQuery
{
    Task<JevResponse?> QueryAsync(string query, Conversation conversation, CancellationToken ct = default);
}

public sealed class LauncherEngine
{
    private readonly IReadOnlyList<Candidate> _index;
    private readonly IJevQuery _jev;
    private readonly Stats _stats;
    private readonly Func<LauncherContext> _context;
    private readonly List<string> _recentApps = new();

    private int _sequence;
    private int _applied;
    private JevResponse? _last;
    private string _lastQuery = string.Empty;

    public LauncherEngine(IReadOnlyList<Candidate> index, IJevQuery jev, Stats stats, Func<LauncherContext> context)
    {
        _index = index;
        _jev = jev;
        _stats = stats;
        _context = context;
    }

    public Stats Stats => _stats;
    public bool LastWasStale { get; private set; }

    public IReadOnlyList<ScoredCandidate> Update(string query)
    {
        _lastQuery = query;
        if (string.IsNullOrWhiteSpace(query)) return Array.Empty<ScoredCandidate>();
        var candidates = Prefilter.BuildCandidates(query, _index, 15);
        return Ranker.Rank(candidates, _last);
    }

    public async Task<IReadOnlyList<ScoredCandidate>> UpdateAsync(string query)
    {
        _lastQuery = query;
        if (string.IsNullOrWhiteSpace(query)) return Array.Empty<ScoredCandidate>();

        var candidates = Prefilter.BuildCandidates(query, _index, 15);
        var local = Ranker.Rank(candidates, _last);

        var seq = Interlocked.Increment(ref _sequence);
        var conversation = new Conversation(candidates, _context());
        var started = Environment.TickCount64;
        var response = await _jev.QueryAsync(query, conversation).ConfigureAwait(false);
        var elapsed = Environment.TickCount64 - started;

        if (seq < _applied)
        {
            LastWasStale = true;
            return local;
        }

        _applied = seq;
        LastWasStale = false;

        if (response is not null)
        {
            _last = response;
            _stats.Record(elapsed, response.InputTokens);
        }

        if (_lastQuery != query) return local;
        return Ranker.Rank(candidates, _last);
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test --filter LauncherEngineTests`
Expected: PASS. If `Stale_response_is_discarded` is flaky, make the fake handler
delay deterministically by key ordering (longer on `"notes"`).

**Step 5: Commit**

```powershell
git add src/JevLauncher.Core/LauncherEngine.cs tests/JevLauncher.Tests/LauncherEngineTests.cs
git commit -m "feat(core): orchestrate queries with stale handling"
```

---

## Task 15: WPF shell — frameless panel, hotkey, tray

**Files:**
- Modify: `src/JevLauncher.App/App.xaml`, `App.xaml.cs`
- Create: `src/JevLauncher.App/PanelWindow.xaml`, `PanelWindow.xaml.cs`
- Create: `src/JevLauncher.App/HotKey.cs`
- Create: `src/JevLauncher.App/TrayIcon.cs`
- Create: `src/JevLauncher.App/Acrylic.cs`

**Step 1: Add tray dependency**

```powershell
dotnet add src/JevLauncher.App/JevLauncher.App.csproj package H.NotifyIcon.Wpf
```

**Step 2: Implement the global hotkey**

```csharp
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using JevLauncher.Core.Native;

namespace JevLauncher.App;

public sealed class HotKey : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const int MOD_ALT = 0x0001;
    private const uint VK_SPACE = 0x20;

    private readonly IntPtr _hwnd;
    private readonly HwndSource _source;
    private readonly Action _onPressed;
    private bool _registered;

    public HotKey(Window window, Action onPressed)
    {
        _onPressed = onPressed;
        _hwnd = new WindowInteropHelper(window).Handle;
        _source = HwndSource.FromHwnd(_hwnd)!;
        _source.AddHook(WndProc);
        _registered = RegisterHotKey(_hwnd, 1, MOD_ALT, VK_SPACE);
    }

    public bool IsRegistered => _registered;

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY) { _onPressed(); handled = true; }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_registered) UnregisterHotKey(_hwnd, 1);
        _source.RemoveHook(WndProc);
    }

    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, uint vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
```

**Step 3: Implement acrylic backdrop**

```csharp
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace JevLauncher.App;

public static class Acrylic
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

    public static void Apply(Window window, bool dark)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        var darkVal = dark ? 1 : 0;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkVal, sizeof(int));

        var corner = 2; // round
        DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));

        var backdrop = 3; // Acrylic
        DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
}
```

**Step 4: Panel window XAML**

```xml
<Window x:Class="JevLauncher.App.PanelWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        WindowStyle="None" AllowsTransparency="False" ShowInTaskbar="False"
        Topmost="True" ResizeMode="NoResize" SizeToContent="Height"
        Width="640" Background="#01000000" WindowStartupLocation="Manual">
  <Border CornerRadius="12" Background="#E6111118" Padding="0">
    <StackPanel>
      <TextBox x:Name="QueryBox" FontSize="22" BorderThickness="0"
               Background="Transparent" Padding="16,12" Foreground="#F0F0F5"
               CaretBrush="#F0F0F5" TextChanged="OnQueryChanged"/>
      <ItemsControl x:Name="Rows" MaxHeight="308"/>
      <Grid x:Name="Footer" Height="36" Margin="16,0">
        <TextBlock x:Name="FooterLeft" Foreground="#8A8A99" VerticalAlignment="Center"/>
        <TextBlock x:Name="FooterRight" Foreground="#8A8A99" HorizontalAlignment="Right" VerticalAlignment="Center"/>
      </Grid>
    </StackPanel>
  </Border>
</Window>
```

**Step 5: Panel window code-behind**

Implement:
- `ShowPanel()`: center horizontally, ~20% from top, focus `QueryBox`, apply `Acrylic.Apply`.
- `Window_Deactivated`: hide with a 150ms `DispatcherTimer` guard, cancelled if activation returns.
- `OnQueryChanged`: call `engine.Update(query)` synchronously for immediate fuzzy rows, then `await engine.UpdateAsync(query)` and re-render on the UI thread only if the query hasn't changed.
- `PreviewKeyDown`: Up/Down move selection, Enter runs `Executor.Launch` (or `SystemToggles.Execute` when `SystemToggle`), Esc hides.
- Row rendering: icon via `SHGetFileInfo` for app/file, Segoe Fluent glyph otherwise; green ↵ badge on the top row when `IsTopReady`.
- Footer: `{Stats.LastMs:0} ms · ${Stats.EstimatedCost:0.00000}`; tooltip p50/p95/decisions/tokens.

**Step 6: Tray + App startup**

```csharp
using H.NotifyIcon;
using System.Windows;

namespace JevLauncher.App;

public partial class App : Application
{
    private PanelWindow? _panel;
    private HotKey? _hotKey;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _panel = new PanelWindow();

        _hotKey = new HotKey(_panel, () => _panel.Toggle());
        if (!_hotKey.IsRegistered)
            MessageBox.Show("Alt+Space is already in use. Change the hotkey in Settings.");

        _panel.CreateTrayIcon();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotKey?.Dispose();
        base.OnExit(e);
    }
}
```

Wire the tray icon (`H.NotifyIcon`) with a menu containing Toggle Launcher,
Settings…, Quit, and no Dock/taskbar presence.

**Step 7: Build and run manually**

Run: `dotnet run --project src/JevLauncher.App`
Expected: App starts with no window; Alt+Space shows the panel; Esc hides it; the
tray icon offers Toggle/Settings/Quit.

**Step 8: Commit**

```powershell
git add src/JevLauncher.App
git commit -m "feat(app): frameless panel with hotkey and tray"
```

---

## Task 16: Settings and API key

**Files:**
- Create: `src/JevLauncher.App/Settings.cs`
- Create: `src/JevLauncher.App/SettingsWindow.xaml`, `SettingsWindow.xaml.cs`

**Step 1: Implement settings storage**

- Path: `%APPDATA%\JevLauncher\settings.json`.
- Fields: `ApiKey` (DPAPI-encrypted, `ProtectedData.Protect`), `HotKey`,
  `SearchTemplate`.
- Read `TYPESAFE_API_KEY` from the environment first; fall back to settings.

**Step 2: Settings window**

Small dialog: masked API key field, hotkey capture box, search URL template, and
a "Test connection" button that runs one `JevClient.QueryAsync` and reports the
round-trip.

**Step 3: Empty-state message**

When `!client.HasKey`, the panel shows
`TYPESAFE_API_KEY is not set — local matching only`.

**Step 4: Build and verify manually**

Run: `dotnet run --project src/JevLauncher.App`
Expected: With no key, the panel works as a fuzzy launcher and shows the message.
After saving a key, Jev re-ranks and the footer shows latency and cost.

**Step 5: Commit**

```powershell
git add src/JevLauncher.App
git commit -m "feat(app): settings, API key storage and empty-state messaging"
```

---

## Task 17: run.ps1 and integration harness

**Files:**
- Create: `run.ps1`
- Create: `tools/JevProbe/JevProbe.csproj`, `tools/JevProbe/Program.cs`

**Step 1: Write run.ps1**

```powershell
param([switch]$Show)
$ErrorActionPreference = 'Stop'
if (-not $env:TYPESAFE_API_KEY) {
  Write-Host 'TYPESAFE_API_KEY is not set — launcher will run in local matching only mode.'
}
dotnet run --project src/JevLauncher.App
```

**Step 2: Write the probe CLI**

A tiny console app that takes a query, builds a fixed candidate set from
`LocalIndex.Build()`, calls `JevClient.QueryAsync`, and prints the target
distribution, action, `ready`, latency and tokens. This mirrors the original's
`curl` harness for iterating on question wording.

**Step 3: Verify against the live API**

Run: `$env:TYPESAFE_API_KEY='...'; dotnet run --project tools/JevProbe -- "the pdf I just downloaded"`
Expected: a target probability sorted list, `ready` value, latency in ms, and
input token count.

**Step 4: Commit**

```powershell
git add run.ps1 tools
git commit -m "chore: add run script and Jev probe CLI"
```

---

## Task 18: Final verification

**Step 1: Run the full test suite**

Run: `dotnet test`
Expected: all tests pass, no network access.

**Step 2: Manual smoke test**

1. `.\run.ps1`
2. Alt+Space, type `dark` → top row "Toggle Dark Mode" with green ↵; Enter flips it.
3. Type `wifi off` → Wi-Fi toggle is top; Enter attempts the toggle (admin note if needed).
4. Type `15% of 240` → `= 36`; Enter copies it.
5. Type `the pdf I just downloaded` → newest PDF is top; Enter opens it.
6. Type `sleep` → Sleep is top.
7. Unset the key, restart → fuzzy-only mode with the empty-state message.

**Step 3: Commit any fixes**

```powershell
git add -A
git commit -m "fix: address smoke-test findings"
```
