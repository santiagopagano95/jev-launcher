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

    private static (LauncherEngine Engine, CountingJev Jev, LauncherServices Services) New()
    {
        var index = new List<Candidate>
        {
            new("f0", CandidateKind.OpenFile, "budget.pdf", "PDF in Documents · modified 2 h ago", "budget.pdf", "path0"),
            new("a0", CandidateKind.OpenApp, "Budget App", "Application", "Budget App", "app0"),
            new("t0", CandidateKind.SystemToggle, "Toggle Dark Mode", "System toggle", "dark light mode", "dark-mode"),
        };
        var jev = new CountingJev();
        var services = new LauncherServices();
        var engine = new LauncherEngine(index, jev, new Stats(),
            () => new LauncherContext("x", Array.Empty<string>(), "text", "afternoon", "Friday"), services);
        return (engine, jev, services);
    }

    [Fact]
    public void Web_command_builds_an_open_url_candidate()
    {
        var (engine, _, _) = New();
        var rows = engine.Update("/yt lofi beats");
        Assert.Equal(CandidateKind.OpenUrl, rows[0].Candidate.Kind);
        Assert.Equal("https://www.youtube.com/results?search_query=lofi%20beats", rows[0].Candidate.Target);
    }

    [Fact]
    public void Web_command_without_argument_opens_the_home_page()
    {
        var (engine, _, _) = New();
        var rows = engine.Update("/netflix");
        Assert.Equal(CandidateKind.OpenUrl, rows[0].Candidate.Kind);
        Assert.Equal("https://www.netflix.com", rows[0].Candidate.Target);
    }

    [Fact]
    public void Slash_alone_opens_the_palette()
    {
        var (engine, _, _) = New();
        var rows = engine.Update("/");
        Assert.NotEmpty(rows);
        Assert.All(rows, r => Assert.Equal(CandidateKind.Command, r.Candidate.Kind));
        Assert.Contains(rows, r => r.Candidate.Id == "cmd:web");
    }

    [Fact]
    public void Partial_command_name_filters_the_palette()
    {
        var (engine, _, _) = New();
        var rows = engine.Update("/tube");
        Assert.Contains(rows, r => r.Candidate.Id == "cmd:yt");
    }

    [Fact]
    public void File_command_scopes_to_files()
    {
        var (engine, _, _) = New();
        var rows = engine.Update("/file budget");
        Assert.Contains(rows, r => r.Candidate.Id == "f0");
        Assert.DoesNotContain(rows, r => r.Candidate.Id == "a0");
        Assert.DoesNotContain(rows, r => r.Candidate.Id == "t0");
    }

    [Fact]
    public void Toggle_command_scopes_to_toggles()
    {
        var (engine, _, _) = New();
        var rows = engine.Update("/toggle dark");
        Assert.Contains(rows, r => r.Candidate.Id == "t0");
        Assert.DoesNotContain(rows, r => r.Candidate.Kind == CandidateKind.OpenFile);
    }

    [Fact]
    public void Recent_command_lists_recent_files()
    {
        var (engine, _, _) = New();
        var rows = engine.Update("/recent");
        Assert.Contains(rows, r => r.Candidate.Id == "f0");
    }

    [Fact]
    public void App_action_command_returns_an_app_candidate()
    {
        var (engine, _, _) = New();
        var rows = engine.Update("/settings");
        Assert.Equal("app:settings", rows[0].Candidate.Id);
    }

    [Fact]
    public void Snippet_scope_lists_snippets()
    {
        var (engine, _, services) = New();
        services.Snippets = new[] { new Snippet("email", "santi@example.com") };
        var rows = engine.Update("/snip");
        Assert.Contains(rows, r => r.Candidate.Kind == CandidateKind.Copy && r.Candidate.Target == "santi@example.com");
    }

    [Fact]
    public void Clipboard_scope_lists_history()
    {
        var (engine, _, services) = New();
        services.Clipboard.Push("hola");
        var rows = engine.Update("/clip");
        Assert.Contains(rows, r => r.Candidate.Target == "hola");
    }

    [Fact]
    public void Utility_scope_builds_a_uuid()
    {
        var (engine, _, _) = New();
        var rows = engine.Update("/uuid");
        Assert.Equal(CandidateKind.Copy, rows[0].Candidate.Kind);
        Assert.True(Guid.TryParse(rows[0].Candidate.Target, out _));
    }

    [Fact]
    public void Window_scope_uses_the_provider()
    {
        var (engine, _, services) = New();
        services.Windows = () => new[]
        {
            new Candidate("win:1", CandidateKind.FocusWindow, "Visual Studio Code", "Window", "Visual Studio Code", "1"),
        };
        var rows = engine.Update("/win code");
        Assert.Contains(rows, r => r.Candidate.Kind == CandidateKind.FocusWindow);
    }

    [Fact]
    public void Note_with_text_builds_a_save_candidate()
    {
        var (engine, _, _) = New();
        var rows = engine.Update("/note comprar cafe");
        Assert.Equal(CandidateKind.SaveNote, rows[0].Candidate.Kind);
        Assert.Equal("comprar cafe", rows[0].Candidate.Target);
    }

    [Fact]
    public void Timer_with_a_duration_builds_a_timer_candidate()
    {
        var (engine, _, _) = New();
        var rows = engine.Update("/timer 5m");
        Assert.Equal(CandidateKind.Timer, rows[0].Candidate.Kind);
        Assert.Equal("300000", rows[0].Candidate.Target);
    }

    [Fact]
    public async Task Command_queries_do_not_call_jev()
    {
        var (engine, jev, _) = New();
        await engine.UpdateAsync("/yt lofi");
        Assert.Equal(0, jev.Calls);
    }
}
