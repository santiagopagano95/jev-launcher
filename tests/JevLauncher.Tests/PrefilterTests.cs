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
