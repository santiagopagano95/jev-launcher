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
            new("f0", CandidateKind.OpenFile, "WhatsApp Image 2026-07-08 at 15.23.32.jpeg", "JPEG", "image 15 2", "x"),
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
            new("d0", CandidateKind.OpenFile, "MR_LOGO_DARK.png", "PNG", "logo dark", "x"),
            new("t0", CandidateKind.SystemToggle, "Toggle Dark Mode", "System toggle", "dark light mode appearance theme", "dark-mode"),
        };

        var rows = Engine(index).Update("dark");

        Assert.Equal("dark-mode", rows[0].Candidate.Target);
    }
}
