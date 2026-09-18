namespace JevLauncher.Core;

public sealed class Stats
{
    public const double DollarsPerMillionInputTokens = 0.042;

    private readonly List<double> _latencies = new();
    private long _inputTokens;

    public double LastMs { get; private set; }
    public int Decisions { get; private set; }
    public long InputTokens => _inputTokens;

    public double P50 => Percentile(0.50);
    public double P95 => Percentile(0.95);
    public double EstimatedCost => _inputTokens / 1_000_000.0 * DollarsPerMillionInputTokens;

    public void Record(double milliseconds, int inputTokens)
    {
        LastMs = milliseconds;
        Decisions++;
        _inputTokens += inputTokens;
        _latencies.Add(milliseconds);
    }

    private double Percentile(double p)
    {
        if (_latencies.Count == 0) return 0;
        var sorted = _latencies.OrderBy(x => x).ToList();
        var idx = (int)Math.Ceiling(p * sorted.Count) - 1;
        return sorted[Math.Clamp(idx, 0, sorted.Count - 1)];
    }
}
