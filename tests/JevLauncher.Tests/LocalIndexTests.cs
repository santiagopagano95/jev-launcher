using JevLauncher.Core;
using Xunit;

public class LocalIndexTests
{
    [Fact]
    public void Start_menu_shortcut_becomes_open_app_candidate()
    {
        var c = LocalIndex.AppCandidateFromShortcut("Google Chrome.lnk", @"C:\...\Google Chrome.lnk");
        Assert.Equal(CandidateKind.OpenApp, c.Kind);
        Assert.Equal("Google Chrome", c.Title);
    }

    [Fact]
    public void File_candidate_includes_recency_detail()
    {
        var age = DateTime.Now.AddMinutes(-16);
        var c = LocalIndex.FileCandidateFromPath(@"C:\Users\me\Downloads\Q3-Roadmap-Review.pdf", age);
        Assert.Contains("modified", c.Detail);
        Assert.Contains("PDF", c.Detail);
        Assert.Equal(CandidateKind.OpenFile, c.Kind);
    }
}
