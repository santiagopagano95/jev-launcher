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
}
