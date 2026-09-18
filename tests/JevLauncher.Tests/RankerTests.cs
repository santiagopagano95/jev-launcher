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
