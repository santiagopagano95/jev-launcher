using JevLauncher.Core;
using Xunit;

public class GlobalFileSearchTests
{
    [Fact]
    public void Builds_a_windows_search_query()
    {
        var sql = GlobalFileSearch.BuildSql("budget", 20);

        Assert.Contains("SystemIndex", sql);
        Assert.Contains("System.ItemPathDisplay", sql);
        Assert.Contains("LIKE '%budget%'", sql);
        Assert.StartsWith("SELECT TOP 20", sql);
    }

    [Fact]
    public void Sanitizes_single_quotes()
    {
        Assert.Equal("O''Brien", GlobalFileSearch.Sanitize("O'Brien"));
    }

    [Fact]
    public void Empty_query_returns_nothing()
    {
        Assert.Empty(GlobalFileSearch.Search("  "));
    }
}
