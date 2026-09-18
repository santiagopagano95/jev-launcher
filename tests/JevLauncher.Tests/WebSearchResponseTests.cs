using JevLauncher.Core;
using Xunit;

public class WebSearchResponseTests
{
    private const string Json = """
    {
      "web": {
        "results": [
          { "title": "TypeSafe AI", "url": "https://typesafe.ai/", "description": "Machine-native intelligence." },
          { "title": "Brave Search API", "url": "https://brave.com/search/api/", "description": "Web search API." },
          { "title": "no url here", "description": "skipped" }
        ]
      }
    }
    """;

    [Fact]
    public void Parses_title_url_and_description()
    {
        var results = WebSearchResponse.Parse(Json);
        Assert.Equal(2, results.Count);
        Assert.Equal("TypeSafe AI", results[0].Title);
        Assert.Equal("https://typesafe.ai/", results[0].Url);
        Assert.Equal("Machine-native intelligence.", results[0].Description);
    }

    [Fact]
    public void Skips_entries_without_a_url()
    {
        Assert.DoesNotContain(WebSearchResponse.Parse(Json), r => r.Title == "no url here");
    }

    [Fact]
    public void Missing_web_section_is_empty()
    {
        Assert.Empty(WebSearchResponse.Parse("{}"));
        Assert.Empty(WebSearchResponse.Parse("""{ "web": {} }"""));
    }
}
