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

    [Fact]
    public void Scope_filter_excludes_other_kinds()
    {
        var all = new List<Candidate>
        {
            C("f0", "budget.pdf", kind: CandidateKind.OpenFile),
            C("a0", "Budget App", kind: CandidateKind.OpenApp),
            C("t0", "Toggle Dark Mode", "dark", CandidateKind.SystemToggle),
        };

        var files = Prefilter.BuildCandidates("budget", all, 15, CandidateKind.OpenFile);
        Assert.Contains(files, c => c.Id == "f0");
        Assert.DoesNotContain(files, c => c.Kind == CandidateKind.OpenApp);
        Assert.DoesNotContain(files, c => c.Kind == CandidateKind.SystemToggle);

        var apps = Prefilter.BuildCandidates("budget", all, 15, CandidateKind.OpenApp);
        Assert.Contains(apps, c => c.Id == "a0");
        Assert.DoesNotContain(apps, c => c.Kind == CandidateKind.OpenFile);
    }
}
