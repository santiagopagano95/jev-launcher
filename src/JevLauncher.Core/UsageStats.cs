using System.IO;
using System.Text.Json;

namespace JevLauncher.Core;

public sealed record UsageEntry(int Count, DateTime LastUsed);

/// <summary>Learns which entries the user actually opens and boosts them over time.</summary>
public sealed class UsageStats
{
    private const double BoostWindowDays = 30;
    private const double ExpectedMaxUses = 20;

    private readonly Dictionary<string, UsageEntry> _entries = new();
    private readonly string? _path;

    public UsageStats(string? path = null)
    {
        _path = path;
        Load();
    }

    public IReadOnlyDictionary<string, UsageEntry> Entries => _entries;

    public void Record(string? id, DateTime? when = null)
    {
        if (string.IsNullOrWhiteSpace(id)) return;

        var now = when ?? DateTime.UtcNow;
        var entry = _entries.TryGetValue(id, out var current)
            ? current with { Count = current.Count + 1, LastUsed = now }
            : new UsageEntry(1, now);

        _entries[id] = entry;
        Save();
    }

    /// <summary>0..1 relevance boost from frequency and recency.</summary>
    public double Boost(string? id, DateTime? now = null)
    {
        if (string.IsNullOrWhiteSpace(id) || !_entries.TryGetValue(id, out var entry)) return 0;

        var frequency = Math.Log(1 + entry.Count) / Math.Log(1 + ExpectedMaxUses);
        var ageDays = ((now ?? DateTime.UtcNow) - entry.LastUsed).TotalDays;
        var recency = Math.Exp(-Math.Max(0, ageDays) / BoostWindowDays);
        return Math.Clamp(0.5 * frequency + 0.5 * recency, 0, 1);
    }

    public IReadOnlyList<KeyValuePair<string, UsageEntry>> Top(int max = 10) =>
        _entries
            .OrderByDescending(e => e.Value.Count)
            .ThenByDescending(e => e.Value.LastUsed)
            .Take(max)
            .ToList();

    private void Load()
    {
        if (string.IsNullOrEmpty(_path) || !File.Exists(_path)) return;

        try
        {
            var loaded = JsonSerializer.Deserialize<Dictionary<string, UsageEntry>>(File.ReadAllText(_path));
            if (loaded is null) return;

            foreach (var pair in loaded) _entries[pair.Key] = pair.Value;
        }
        catch
        {
            // A corrupt usage file is not worth failing over.
        }
    }

    private void Save()
    {
        if (string.IsNullOrEmpty(_path)) return;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(_entries));
        }
        catch
        {
            // Best effort.
        }
    }
}
