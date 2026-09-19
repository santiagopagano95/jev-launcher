using JevLauncher.Core;
using Xunit;

public class CommandCandidatesTests
{
    [Fact]
    public void Palette_rows_insert_the_command_text()
    {
        var rows = CommandCandidates.Palette(string.Empty);
        var web = rows.First(r => r.Id == "cmd:web");
        Assert.Equal(CandidateKind.Command, web.Kind);
        Assert.Equal("/web ", web.Target);
    }

    [Fact]
    public void Palette_search_matches_titles()
    {
        var rows = CommandCandidates.Palette("tube");
        Assert.Contains(rows, r => r.Id == "cmd:yt");
    }

    [Fact]
    public void Palette_search_matches_aliases()
    {
        var rows = CommandCandidates.Palette("toggles");
        Assert.Contains(rows, r => r.Id == "cmd:toggle");
    }

    [Fact]
    public void Web_action_builds_the_command_url()
    {
        var command = CommandParser.Resolve("yt")!;
        var candidate = CommandCandidates.Web(command, "lofi beats");
        Assert.Equal(CandidateKind.OpenUrl, candidate.Kind);
        Assert.Equal("https://www.youtube.com/results?search_query=lofi%20beats", candidate.Target);
    }

    [Fact]
    public void App_action_ids_use_a_distinct_prefix()
    {
        var action = CommandCandidates.AppAction(CommandParser.Resolve("settings")!);
        Assert.StartsWith("action:", action.Id);
    }

    [Fact]
    public void Indexed_apps_are_not_mistaken_for_app_actions()
    {
        var app = LocalIndex.AppCandidateFromShortcut("Mensi.lnk", @"C:\x\Mensi.lnk");
        Assert.False(CommandCandidates.IsAppAction(app));

        var action = CommandCandidates.AppAction(CommandParser.Resolve("quit")!);
        Assert.True(CommandCandidates.IsAppAction(action));
    }

    [Fact]
    public void Home_builds_an_open_url_for_the_home_page()
    {
        var command = CommandParser.Resolve("netflix")!;
        var candidate = CommandCandidates.Home(command);
        Assert.Equal(CandidateKind.OpenUrl, candidate.Kind);
        Assert.Equal("https://www.netflix.com", candidate.Target);
    }
}
