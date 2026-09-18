using System.Globalization;
using System.IO;

namespace JevLauncher.Core;

public static class Prefilter
{
    public static IReadOnlyList<Candidate> TopMatches(string query, IEnumerable<Candidate> all, int n)
    {
        return all
            .Select(c => c with { Fuzzy = Fuzzy.Score(query, c.Title, c.Keywords) })
            .Where(c => c.Fuzzy > 0)
            .OrderByDescending(c => c.Fuzzy)
            .Take(n)
            .ToList();
    }

    public static IReadOnlyList<Candidate> BuildCandidates(string query, IEnumerable<Candidate> all, int cap)
    {
        var allList = all as IReadOnlyList<Candidate> ?? all.ToList();

        var list = TopMatches(query, allList, Math.Max(0, cap - 2)).ToList();
        var seen = list.Select(c => c.Id).ToHashSet();

        // Reserve room for the calculator and the web fallback, then always offer
        // the most recently modified local files. Natural-language queries such as
        // "the pdf I just downloaded" would otherwise have no file for Jev to pick.
        // When the query names a file type, recent files of that type come first.
        var recentSlots = Math.Max(0, cap - list.Count - 2);
        if (recentSlots > 0)
        {
            foreach (var file in OrderedRecentFiles(allList, query))
            {
                if (recentSlots <= 0) break;
                if (seen.Add(file.Id))
                {
                    list.Add(file);
                    recentSlots--;
                }
            }
        }

        if (Calculator.TryParse(query, out var value))
        {
            var text = value.ToString("0.######", CultureInfo.InvariantCulture);
            list.Add(new Candidate("calc", CandidateKind.Calculate, $"= {text}",
                "Press Enter to copy", "calculator calc math", text, Fuzzy: 1000));
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            list.Add(new Candidate("web", CandidateKind.WebSearch,
                $"Search the web for “{query}”", "Opens your default browser",
                "web search google bing", query));
        }

        return list.Take(cap).ToList();
    }

    private static IReadOnlyList<Candidate> OrderedRecentFiles(IReadOnlyList<Candidate> all, string query)
    {
        var tokens = query.ToLowerInvariant()
            .Split(new[] { ' ', '.', ',', '!', '?', ';', ':', '(', ')', '/', '\\', '-', '_' },
                StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet();

        // The index is ordered by recency (newest first); a stable sort keeps that
        // order within files that match the query's file-type hint.
        return all
            .Where(c => c.Kind == CandidateKind.OpenFile)
            .OrderByDescending(f => ExtensionMatches(f.Title, tokens))
            .ToList();
    }

    private static bool ExtensionMatches(string title, HashSet<string> tokens)
    {
        var ext = Path.GetExtension(title).TrimStart('.').ToLowerInvariant();
        if (ext.Length == 0) return false;

        return tokens.Contains(ext)
               || tokens.Any(t => t.Length >= 2 &&
                                  (ext.StartsWith(t, StringComparison.Ordinal) ||
                                   t.StartsWith(ext, StringComparison.Ordinal)));
    }

    public static string AssignIds(IReadOnlyList<Candidate> candidates)
    {
        var map = candidates
            .Select((c, i) => (Id: $"c{i}", c.Target, c.Title))
            .ToDictionary(x => x.Id, x => x.Title);
        return string.Join(", ", map.Keys);
    }
}
