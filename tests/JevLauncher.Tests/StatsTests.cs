using JevLauncher.Core;
using Xunit;

public class StatsTests
{
    [Fact]
    public void Tracks_last_p50_p95_and_cost()
    {
        var s = new Stats();
        s.Record(100, 1400);
        s.Record(200, 1400);
        s.Record(300, 1400);
        Assert.Equal(300, s.LastMs);
        Assert.Equal(200, s.P50);
        Assert.Equal(300, s.P95);
        Assert.Equal(3, s.Decisions);
        Assert.Equal(4200, s.InputTokens);
        Assert.Equal(4200 / 1_000_000.0 * 0.042, s.EstimatedCost, 6);
    }

    [Fact]
    public void Empty_stats_are_zero()
    {
        var s = new Stats();
        Assert.Equal(0, s.LastMs);
        Assert.Equal(0, s.P50);
        Assert.Equal(0, s.Decisions);
        Assert.Equal(0, s.EstimatedCost);
    }
}
