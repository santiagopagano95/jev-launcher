using System.Diagnostics;
using System.IO;

namespace JevLauncher.Core;

public static class Executor
{
    public static string DefaultSearchTemplate => "https://www.google.com/search?q={0}";

    public static string BuildSearchUrl(string query, string template)
        => string.Format(template, Uri.EscapeDataString(query));

    public static string? ClipboardTextFor(Candidate candidate)
        => candidate.Kind == CandidateKind.Calculate ? candidate.Target : null;

    public static void Launch(Candidate candidate, Action<string> setClipboard, string? searchTemplate = null)
    {
        switch (candidate.Kind)
        {
            case CandidateKind.Calculate:
                if (candidate.Target is { } text) setClipboard(text);
                break;
            case CandidateKind.Copy:
                if (candidate.Target is { } copyText) setClipboard(copyText);
                break;
            case CandidateKind.FocusWindow:
                WindowList.Focus(candidate.Target);
                break;
            case CandidateKind.WebSearch:
                ShellExecute(BuildSearchUrl(candidate.Target ?? string.Empty, searchTemplate ?? DefaultSearchTemplate));
                break;
            case CandidateKind.OpenApp:
            case CandidateKind.OpenFile:
            case CandidateKind.RunShortcut:
            case CandidateKind.OpenUrl:
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

    public static void OpenContainingFolder(string path)
    {
        var folder = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(folder)) ShellExecute(folder);
    }

    public static void RevealInExplorer(string path)
    {
        if (Directory.Exists(path))
        {
            ShellExecute(path);
            return;
        }

        if (File.Exists(path))
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
    }
}
