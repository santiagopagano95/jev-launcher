using System.Text;

namespace JevLauncher.Core;

public static class Fuzzy
{
    private static readonly HashSet<string> Stopwords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "a", "an", "of", "to", "my", "i", "just", "and", "for", "in", "on",
        "is", "it", "this", "that", "please", "can", "you",
    };

    public static double Score(string query, string title, string keywords)
    {
        var q = Normalize(query);
        if (q.Length == 0) return 0;

        var titleNorm = Normalize(title);
        var keywordNorm = Normalize(keywords);

        return Math.Max(ScoreOne(q, titleNorm, isTitle: true), ScoreOne(q, keywordNorm, isTitle: false));
    }

    private static double ScoreOne(string q, string text, bool isTitle)
    {
        if (text.Length == 0) return 0;
        if (text == q) return 100;

        var weight = isTitle ? 1.0 : 0.9;

        if (text.StartsWith(q, StringComparison.Ordinal)) return 80 * weight;

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Any(w => w == q)) return 70 * weight;

        var initials = string.Concat(words.Select(w => w[0]));
        if (initials.Contains(q, StringComparison.Ordinal)) return 60 * weight;

        if (words.Any(w => w.StartsWith(q, StringComparison.Ordinal))) return 55 * weight;

        var sub = SubsequenceScore(q, text);
        return sub * weight;
    }

    private static double SubsequenceScore(string q, string text)
    {
        int qi = 0, gaps = 0;
        for (var ti = 0; ti < text.Length && qi < q.Length; ti++)
        {
            if (text[ti] == q[qi]) qi++;
            else if (qi > 0) gaps++;
        }
        if (qi < q.Length) return 0;
        return Math.Max(1, 40 - gaps);
    }

    private static string Normalize(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s.ToLowerInvariant())
            sb.Append(char.IsLetterOrDigit(ch) ? ch : ' ');

        var words = sb.ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => !Stopwords.Contains(w));

        return string.Join(' ', words);
    }
}
