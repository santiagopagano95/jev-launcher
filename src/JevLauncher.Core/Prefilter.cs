using System.Globalization;

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
        var list = TopMatches(query, all, Math.Max(0, cap - 2)).ToList();

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

    public static string AssignIds(IReadOnlyList<Candidate> candidates)
    {
        var map = candidates
            .Select((c, i) => (Id: $"c{i}", c.Target, c.Title))
            .ToDictionary(x => x.Id, x => x.Title);
        return string.Join(", ", map.Keys);
    }
}
