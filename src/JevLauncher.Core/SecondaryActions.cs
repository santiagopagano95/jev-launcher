using System.IO;

namespace JevLauncher.Core;

/// <summary>Extra actions offered for a result (Ctrl+K).</summary>
public static class SecondaryActions
{
    public static IReadOnlyList<Candidate> For(Candidate candidate)
    {
        var target = candidate.Target;
        var isUrl = target is { Length: > 0 } &&
                    (target.StartsWith("http", StringComparison.OrdinalIgnoreCase) ||
                     target.StartsWith("spotify:", StringComparison.OrdinalIgnoreCase));
        var isPath = target is { Length: > 0 } &&
                     !target.StartsWith("shell:", StringComparison.OrdinalIgnoreCase) &&
                     !isUrl;
        var existsOnDisk = isPath && (File.Exists(target) || Directory.Exists(target));

        var rows = new List<Candidate>
        {
            Row(candidate, "act:open", "Open", "Run this entry"),
        };

        if (isUrl) rows.Add(Row(candidate, "act:copy", "Copy URL", target!));
        else if (isPath) rows.Add(Row(candidate, "act:copy", "Copy path", target!));

        if (existsOnDisk)
        {
            rows.Add(Row(candidate, "act:folder", "Open containing folder", target!));
            rows.Add(Row(candidate, "act:reveal", "Reveal in Explorer", target!));
        }

        rows.Add(Row(candidate, "act:web", $"Search the web for “{candidate.Title}”", candidate.Title));
        return rows;
    }

    private static Candidate Row(Candidate source, string id, string title, string payload) =>
        new(id, CandidateKind.Command, title, source.Title, string.Empty, payload);
}
