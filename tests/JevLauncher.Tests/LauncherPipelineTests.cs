using JevLauncher.Core;
using Xunit;

public class LauncherPipelineTests
{
    private sealed class NoJev : IJevQuery
    {
        public Task<JevResponse?> QueryAsync(string query, Conversation conversation, CancellationToken ct = default)
            => Task.FromResult<JevResponse?>(null);
    }

    private static LauncherEngine Engine(IReadOnlyList<Candidate> index) =>
        new(index, new NoJev(), new Stats(),
            () => new LauncherContext("test", Array.Empty<string>(), "text", "afternoon", "Thursday"));

    [Fact]
    public void Calculator_result_ranks_first_for_arithmetic_query()
    {
        var index = new List<Candidate>
        {
            new("f0", CandidateKind.OpenFile, "screenshot-2026-07-08.jpeg", "JPEG", "image 15 2", "x"),
            new("t0", CandidateKind.SystemToggle, "Toggle Dark Mode", "System toggle", "dark", "dark-mode"),
        };

        var rows = Engine(index).Update("15+2");

        Assert.Equal(CandidateKind.Calculate, rows[0].Candidate.Kind);
        Assert.Equal("17", rows[0].Candidate.Target);
    }

    [Fact]
    public void Toggle_query_ranks_toggle_first()
    {
        var index = new List<Candidate>
        {
            new("d0", CandidateKind.OpenFile, "brand-logo-dark.png", "PNG", "logo dark", "x"),
            new("t0", CandidateKind.SystemToggle, "Toggle Dark Mode", "System toggle", "dark light mode appearance theme", "dark-mode"),
        };

        var rows = Engine(index).Update("dark");

        Assert.Equal("dark-mode", rows[0].Candidate.Target);
    }

    [Fact]
    public void Natural_language_query_offers_recent_files_as_candidates()
    {
        var index = new List<Candidate>
        {
            new("t0", CandidateKind.SystemToggle, "Toggle Dark Mode", "System toggle", "dark", "dark-mode"),
            new("f0", CandidateKind.OpenFile, "Q3-Roadmap-Review.pdf", "PDF in Downloads · modified 16 min ago", "Q3-Roadmap-Review.pdf", "path0"),
            new("f1", CandidateKind.OpenFile, "old-notes.txt", "TXT in Documents · modified 3 months ago", "old-notes.txt", "path1"),
        };

        var candidates = Prefilter.BuildCandidates("the pdf I just downloaded", index, 15);

        Assert.Contains(candidates, c => c.Id == "f0");
    }

    [Fact]
    public void Type_hint_prefers_recent_files_of_that_type()
    {
        var index = new List<Candidate>
        {
            // Input order is recency: newest first.
            new("newpy", CandidateKind.OpenFile, "_net.py", "PY in Docs", "_net.py", "p"),
            new("doc", CandidateKind.OpenFile, "Q3-Roadmap-Review.pdf", "PDF in Downloads", "Q3-Roadmap-Review.pdf", "p"),
            new("oldpy", CandidateKind.OpenFile, "old_script.py", "PY in Docs", "old_script.py", "p"),
        };

        var candidates = Prefilter.BuildCandidates("the pdf I just downloaded", index, 3);

        Assert.Contains(candidates, c => c.Id == "doc");
        Assert.DoesNotContain(candidates, c => c.Id == "newpy");
    }

    private sealed class FixedJev : IJevQuery
    {
        private readonly JevResponse _response;
        public FixedJev(JevResponse response) => _response = response;
        public Task<JevResponse?> QueryAsync(string query, Conversation conversation, CancellationToken ct = default)
            => Task.FromResult<JevResponse?>(_response);
    }

    [Fact]
    public async Task Jev_picks_the_intended_recent_file()
    {
        var index = new List<Candidate>
        {
            new("f0", CandidateKind.OpenFile, "Q3-Roadmap-Review.pdf", "PDF in Downloads · modified 16 min ago", "Q3-Roadmap-Review.pdf", "path0"),
            new("f1", CandidateKind.OpenFile, "old-notes.txt", "TXT in Documents · modified 3 months ago", "old-notes.txt", "path1"),
        };
        var jev = new FixedJev(new JevResponse("f0",
            new Dictionary<string, double> { ["f0"] = 0.9, ["f1"] = 0.05 },
            "open_file", 0.8, 100, 0));
        var engine = new LauncherEngine(index, jev, new Stats(),
            () => new LauncherContext("x", Array.Empty<string>(), "text", "afternoon", "Thursday"));

        var rows = await engine.UpdateAsync("the pdf I just downloaded");

        Assert.Equal("f0", rows[0].Candidate.Id);
        Assert.True(rows[0].IsTopReady);
    }
}
