using System.IO;

namespace JevLauncher.Core;

public sealed record Snippet(string Name, string Text);

public sealed class ClipboardHistory
{
    public const int DefaultCapacity = 50;

    private readonly List<string> _items = new();
    private readonly int _capacity;

    public ClipboardHistory(int capacity = DefaultCapacity) => _capacity = Math.Max(1, capacity);

    public IReadOnlyList<string> Items => _items;

    public void Push(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        _items.Remove(text);
        _items.Insert(0, text);
        while (_items.Count > _capacity) _items.RemoveAt(_items.Count - 1);
    }
}

public sealed class NotesStore
{
    private readonly string _path;

    public NotesStore(string path) => _path = path;

    public void Append(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.AppendAllText(_path, $"{DateTime.Now:yyyy-MM-dd HH:mm} {text.Trim()}{Environment.NewLine}");
    }

    public IReadOnlyList<string> Recent(int max = 10) =>
        File.Exists(_path)
            ? File.ReadAllLines(_path)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Reverse()
                .Take(max)
                .Select(StripTimestamp)
                .ToList()
            : Array.Empty<string>();

    private static string StripTimestamp(string line) =>
        line.Length > 17 ? line[17..] : line;
}

public static class SnippetCandidates
{
    public static IReadOnlyList<Candidate> Build(IReadOnlyList<Snippet> snippets, string filter)
    {
        IEnumerable<Snippet> matches = snippets;

        if (!string.IsNullOrWhiteSpace(filter))
        {
            matches = snippets
                .Select(s => (Snippet: s, Score: Fuzzy.Score(filter, s.Name, s.Name + " " + s.Text)))
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .Select(x => x.Snippet);
        }

        return matches
            .Select(s => new Candidate("snip:" + s.Name, CandidateKind.Copy, s.Name,
                Preview(s.Text), s.Name + " " + s.Text, s.Text))
            .ToList();
    }

    internal static string Preview(string value)
    {
        var single = value.ReplaceLineEndings(" ");
        return single.Length <= 80 ? single : single[..80] + "…";
    }
}

public static class ClipboardCandidates
{
    public static IReadOnlyList<Candidate> Build(IReadOnlyList<string> items, string filter, int max = 25)
    {
        IEnumerable<string> matches = items;

        if (!string.IsNullOrWhiteSpace(filter))
        {
            matches = items
                .Select(t => (Text: t, Score: Fuzzy.Score(filter, t, string.Empty)))
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .Select(x => x.Text);
        }

        return matches
            .Take(max)
            .Select(t => new Candidate("clip:" + t.GetHashCode(), CandidateKind.Copy,
                SnippetCandidates.Preview(t), "Clipboard", t, t))
            .ToList();
    }
}

/// <summary>Runtime dependencies the engine needs for command scopes.</summary>
public sealed class LauncherServices
{
    public IReadOnlyList<Snippet> Snippets { get; set; } = Array.Empty<Snippet>();
    public ClipboardHistory Clipboard { get; } = new();
    public NotesStore? Notes { get; set; }
    public Func<IReadOnlyList<Candidate>>? Windows { get; set; }
    public IWebSearch? WebSearch { get; set; }
}
