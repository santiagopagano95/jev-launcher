using JevLauncher.Core;
using Xunit;

public class UsageStatsTests
{
    private static string TempPath() =>
        Path.Combine(AppContext.BaseDirectory, $"usage-{Guid.NewGuid():N}.json");

    [Fact]
    public void Unknown_ids_get_no_boost()
    {
        Assert.Equal(0, new UsageStats().Boost("nope"));
    }

    [Fact]
    public void Frequently_used_entries_get_a_higher_boost()
    {
        var usage = new UsageStats();
        usage.Record("app:often");
        usage.Record("app:often");
        usage.Record("app:often");
        usage.Record("app:rare");

        Assert.True(usage.Boost("app:often") > usage.Boost("app:rare"));
    }

    [Fact]
    public void Old_usage_decays()
    {
        var usage = new UsageStats();
        var longAgo = DateTime.UtcNow.AddDays(-120);
        usage.Record("app:old", longAgo);

        Assert.True(usage.Boost("app:old") < 0.2);
    }

    [Fact]
    public void Records_round_trip_through_the_file()
    {
        var path = TempPath();
        try
        {
            var first = new UsageStats(path);
            first.Record("app:kept");
            first.Record("app:kept");

            var second = new UsageStats(path);
            Assert.Equal(2, second.Entries["app:kept"].Count);
            Assert.True(second.Boost("app:kept") > 0);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Top_lists_the_most_used_first()
    {
        var usage = new UsageStats();
        usage.Record("a");
        usage.Record("b");
        usage.Record("b");

        Assert.Equal("b", usage.Top(1)[0].Key);
    }
}
