using JevLauncher.Core;
using Xunit;

public class FuzzyTests
{
    [Fact]
    public void Exact_match_beats_prefix()
    {
        Assert.True(Fuzzy.Score("dark", "dark", "") > Fuzzy.Score("dark", "dark mode", ""));
    }

    [Fact]
    public void Prefix_beats_subsequence()
    {
        Assert.True(Fuzzy.Score("dar", "dark mode", "") > Fuzzy.Score("dar", "dashboard", ""));
    }

    [Fact]
    public void Word_initial_matches()
    {
        Assert.True(Fuzzy.Score("dm", "dark mode", "") > 0);
    }

    [Fact]
    public void Subsequence_matches_in_order_only()
    {
        Assert.True(Fuzzy.Score("dkm", "dark mode", "") > 0);
        Assert.Equal(0, Fuzzy.Score("mkd", "dark mode", ""));
    }

    [Fact]
    public void Keywords_are_searched()
    {
        Assert.True(Fuzzy.Score("wifi", "wireless", "wifi network") > 0);
    }

    [Fact]
    public void Stopwords_are_stripped()
    {
        Assert.Equal(Fuzzy.Score("the pdf", "report", "pdf"), Fuzzy.Score("pdf", "report", "pdf"));
    }

    [Fact]
    public void No_match_is_zero()
    {
        Assert.Equal(0, Fuzzy.Score("zzzz", "dark mode", "toggle"));
    }
}
