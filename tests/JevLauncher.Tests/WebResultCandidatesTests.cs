using JevLauncher.Core;
using Xunit;

public class WebResultCandidatesTests
{
    [Fact]
    public void Builds_open_url_rows_with_domain_and_snippet()
    {
        var results = new[]
        {
            new WebResult("TypeSafe AI", "https://typesafe.ai/docs/start", "Machine-native intelligence for automation."),
        };

        var rows = WebResultCandidates.Build(results);

        Assert.Single(rows);
        Assert.Equal(CandidateKind.OpenUrl, rows[0].Kind);
        Assert.Equal("TypeSafe AI", rows[0].Title);
        Assert.Equal("https://typesafe.ai/docs/start", rows[0].Target);
        Assert.Contains("typesafe.ai", rows[0].Detail);
        Assert.Contains("Machine-native", rows[0].Detail);
    }

    [Fact]
    public void Truncates_long_snippets()
    {
        var rows = WebResultCandidates.Build(new[]
        {
            new WebResult("title", "https://example.com", new string('x', 200)),
        });

        Assert.True(rows[0].Detail.Length < 140);
        Assert.EndsWith("…", rows[0].Detail);
    }

    [Fact]
    public void Falls_back_to_the_url_when_it_is_not_absolute()
    {
        var rows = WebResultCandidates.Build(new[] { new WebResult("t", "not-a-url", "") });
        Assert.Equal("not-a-url", rows[0].Target);
        Assert.Equal("not-a-url", rows[0].Detail);
    }
}
