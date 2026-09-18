using JevLauncher.Core;
using Xunit;

public class CommandEngineTests
{
    private sealed class CountingJev : IJevQuery
    {
        public int Calls;

        public Task<JevResponse?> QueryAsync(string query, Conversation conversation, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult<JevResponse?>(null);
        }
    }

    private static (LauncherEngine Engine, CountingJev Jev) New()
    {
        var index = new List<Candidate>
        {
            new("f0", CandidateKind.OpenFile, "budget.pdf", "PDF in Documents · modified 2 h ago", "budget.pdf", "path0"),
            new("a0", CandidateKind.OpenApp, "Budget App", "Application", "Budget App", "app0"),
            new("t0", CandidateKind.SystemToggle, "Toggle Dark Mode", "System toggle", "dark light mode", "dark-mode"),
        };
        var jev = new CountingJev();
        var engine = new LauncherEngine(index, jev, new Stats(),
            () => new LauncherContext("x", Array.Empty<string>(), "text", "afternoon", "Friday"));
        return (engine, jev);
    }

    [Fact]
    public void Web_command_builds_an_open_url_candidate()
    {
        var (engine, _) = New();
        var rows = engine.Update("/yt lofi beats");
        Assert.Equal(CandidateKind.OpenUrl, rows[0].Candidate.Kind);
        Assert.Equal("https://www.youtube.com/results?search_query=lofi%20beats", rows[0].Candidate.Target);
    }

    [Fact]
    public void Slash_alone_opens_the_palette()
    {
        var (engine, _) = New();
        var rows = engine.Update("/");
        Assert.NotEmpty(rows);
        Assert.All(rows, r => Assert.Equal(CandidateKind.Command, r.Candidate.Kind));
        Assert.Contains(rows, r => r.Candidate.Id == "cmd:web");
    }

    [Fact]
    public void Partial_command_name_filters_the_palette()
    {
        var (engine, _) = New();
        var rows = engine.Update("/tube");
        Assert.Contains(rows, r => r.Candidate.Id == "cmd:yt");
    }

    [Fact]
    public void File_command_scopes_to_files()
    {
        var (engine, _) = New();
        var rows = engine.Update("/file budget");
        Assert.Contains(rows, r => r.Candidate.Id == "f0");
        Assert.DoesNotContain(rows, r => r.Candidate.Id == "a0");
        Assert.DoesNotContain(rows, r => r.Candidate.Id == "t0");
    }

    [Fact]
    public void Toggle_command_scopes_to_toggles()
    {
        var (engine, _) = New();
        var rows = engine.Update("/toggle dark");
        Assert.Contains(rows, r => r.Candidate.Id == "t0");
        Assert.DoesNotContain(rows, r => r.Candidate.Kind == CandidateKind.OpenFile);
    }

    [Fact]
    public void Recent_command_lists_recent_files()
    {
        var (engine, _) = New();
        var rows = engine.Update("/recent");
        Assert.Contains(rows, r => r.Candidate.Id == "f0");
    }

    [Fact]
    public void App_action_command_returns_an_app_candidate()
    {
        var (engine, _) = New();
        var rows = engine.Update("/settings");
        Assert.Equal("app:settings", rows[0].Candidate.Id);
    }

    [Fact]
    public async Task Command_queries_do_not_call_jev()
    {
        var (engine, jev) = New();
        await engine.UpdateAsync("/yt lofi");
        Assert.Equal(0, jev.Calls);
    }
}
