using JevLauncher.Core;
using Xunit;

public class LauncherEngineTests
{
    private sealed class FakeJev : IJevQuery
    {
        public readonly List<string> Queries = new();
        public readonly Dictionary<string, TaskCompletionSource<JevResponse?>> Gates = new();
        public Func<string, JevResponse?> Respond = _ => null;

        public Task<JevResponse?> QueryAsync(string query, Conversation conversation, CancellationToken ct = default)
        {
            Queries.Add(query);
            var tcs = new TaskCompletionSource<JevResponse?>(TaskCreationOptions.RunContinuationsAsynchronously);
            Gates[query] = tcs;
            return tcs.Task;
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
        var engine = NewEngine(new FakeJev());
        var rows = engine.Update("notes");
        Assert.Equal("notes.txt", rows[0].Candidate.Title);
    }

    [Fact]
    public async Task Stale_response_is_discarded()
    {
        var jev = new FakeJev();
        var engine = NewEngine(jev);

        var t1 = engine.UpdateAsync("n");
        var t2 = engine.UpdateAsync("nx");

        // Resolve the newer request first; the older one must then be discarded.
        jev.Gates["nx"].SetResult(null);
        await t2;
        jev.Gates["n"].SetResult(null);
        await t1;

        Assert.True(engine.LastWasStale);
    }
}
