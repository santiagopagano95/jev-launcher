using System.Diagnostics;

namespace JevLauncher.Core;

public static class Executor
{
    public static string DefaultSearchTemplate => "https://www.google.com/search?q={0}";

    public static string BuildSearchUrl(string query, string template)
        => string.Format(template, Uri.EscapeDataString(query));

    public static string? ClipboardTextFor(Candidate candidate)
        => candidate.Kind == CandidateKind.Calculate ? candidate.Target : null;

    public static void Launch(Candidate candidate, Action<string> setClipboard)
    {
        switch (candidate.Kind)
        {
            case CandidateKind.Calculate:
                if (candidate.Target is { } text) setClipboard(text);
                break;
            case CandidateKind.WebSearch:
                ShellExecute(BuildSearchUrl(candidate.Target ?? string.Empty, DefaultSearchTemplate));
                break;
            case CandidateKind.OpenApp:
            case CandidateKind.OpenFile:
            case CandidateKind.RunShortcut:
                if (!string.IsNullOrWhiteSpace(candidate.Target)) ShellExecute(candidate.Target);
                break;
        }
    }

    private static void ShellExecute(string target)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = target,
            UseShellExecute = true,
        });
    }
}
