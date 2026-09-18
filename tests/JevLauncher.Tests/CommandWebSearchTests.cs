using JevLauncher.Core;
using Xunit;

public class CommandWebSearchTests
{
    private sealed class NullJev : IJevQuery
    {
        public Task<JevResponse?> QueryAsync(string query, Conversation conversation, CancellationToken ct = default)
            => Task.FromResult<JevResponse?>(null);
    }

    private sealed class FakeWebSearch : IWebSearch
    {
        public readonly List<string> Queries = new();
        public Func<string, Task<IReadOnlyList<WebResult>>> Respond =
            _ => Task.FromResult<IReadOnlyList<WebResult>>(Array.Empty<WebResult>());

        public Task<IReadOnlyList<WebResult>> SearchAsync(string query, CancellationToken ct = default)
        {
            Queries.Add(query);
            return Respond(query);
        }
    }

    private sealed class GatedWebSearch : IWebSearch
    {
        private readonly Dictionary<string, TaskCompletionSource<IReadOnlyList<WebResult>>> _gates;

        public GatedWebSearch(Dictionary<string, TaskCompletionSource<IReadOnlyList<WebResult>>> gates) => _gates = gates;

        public Task<IReadOnlyList<WebResult>> SearchAsync(string query, CancellationToken ct = default)
        {
            var gate = new TaskCompletionSource<IReadOnlyList<WebResult>>(TaskCreationOptions.RunContinuationsAsynchronously);
            _gates[query] = gate;
            return gate.Task;
        }
    }

    private static (LauncherEngine Engine, LauncherServices Services) New()
    {
        var index = new List<Candidate>
        {
            new("f0", CandidateKind.OpenFile, "budget.pdf", "PDF in Documents", "budget.pdf", "path0"),
        };
        var services = new LauncherServices();
        var engine = new LauncherEngine(index, new NullJev(), new Stats(),
            () => new LauncherContext("x", Array.Empty<string>(), "text", "afternoon", "Friday"), services);
        return (engine, services);
    }

    [Fact]
    public async Task Web_command_shows_inline_results_then_the_browser_action()
    {
        var (engine, services) = New();
        services.WebSearch = new FakeWebSearch
        {
            Respond = _ => Task.FromResult<IReadOnlyList<WebResult>>(new[]
            {
                new WebResult("TypeSafe AI", "https://typesafe.ai/", "Machine-native intelligence."),
                new WebResult("Brave", "https://brave.com/", "Search API."),
            }),
        };

        var rows = await engine.UpdateAsync("/web typesafe");

        Assert.Equal(CandidateKind.OpenUrl, rows[0].Candidate.Kind);
        Assert.Equal("TypeSafe AI", rows[0].Candidate.Title);
        Assert.Equal("https://typesafe.ai/", rows[0].Candidate.Target);
        Assert.Contains(rows, r => r.Candidate.Id == "url:web");
    }

    [Fact]
    public async Task Web_command_falls_back_without_a_provider()
    {
        var (engine, _) = New();
        var rows = await engine.UpdateAsync("/web typesafe");

        Assert.Single(rows);
        Assert.Equal(CandidateKind.OpenUrl, rows[0].Candidate.Kind);
        Assert.StartsWith("https://www.google.com/search", rows[0].Candidate.Target);
    }

    [Fact]
    public void Web_command_is_a_browser_action_before_the_async_result_arrives()
    {
        var (engine, services) = New();
        services.WebSearch = new FakeWebSearch();

        var rows = engine.Update("/web typesafe");

        Assert.Equal(CandidateKind.OpenUrl, rows[0].Candidate.Kind);
        Assert.StartsWith("https://www.google.com/search", rows[0].Candidate.Target);
    }

    [Fact]
    public async Task Web_results_are_cached_per_query()
    {
        var (engine, services) = New();
        var web = new FakeWebSearch();
        services.WebSearch = web;

        await engine.UpdateAsync("/web cache-me");
        await engine.UpdateAsync("/web cache-me");

        Assert.Single(web.Queries);
    }

    [Fact]
    public async Task Stale_web_results_are_discarded()
    {
        var (engine, services) = New();
        var gates = new Dictionary<string, TaskCompletionSource<IReadOnlyList<WebResult>>>();
        services.WebSearch = new GatedWebSearch(gates);

        var slow = engine.UpdateAsync("/web slow");
        var fast = engine.UpdateAsync("/web fast");

        gates["fast"].SetResult(new[] { new WebResult("Fast", "https://fast.example/", "") });
        await fast;
        gates["slow"].SetResult(new[] { new WebResult("Slow", "https://slow.example/", "") });
        await slow;

        Assert.True(engine.LastWasStale);
    }
}
