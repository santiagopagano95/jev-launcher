using JevLauncher.Core;
using Xunit;

public class ExecutorTests
{
    [Fact]
    public void Web_search_builds_default_browser_url()
    {
        var url = Executor.BuildSearchUrl("hello world", "https://www.google.com/search?q={0}");
        Assert.Equal("https://www.google.com/search?q=hello%20world", url);
    }

    [Fact]
    public void Calculate_copies_the_numeric_result()
    {
        var c = new Candidate("calc", CandidateKind.Calculate, "= 36", "", "", "36");
        Assert.Equal("36", Executor.ClipboardTextFor(c));
    }

    [Fact]
    public void Non_calculate_has_no_clipboard_text()
    {
        var c = new Candidate("c0", CandidateKind.OpenApp, "app", "", "", null);
        Assert.Null(Executor.ClipboardTextFor(c));
    }
}
