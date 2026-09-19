using System.IO;

namespace JevLauncher.Core;

public sealed record StoreApp(string Name, string Path);

/// <summary>Store/UWP and classic apps exposed through the shell's AppsFolder.</summary>
public static class StoreApps
{
    public static IReadOnlyList<StoreApp> Enumerate()
    {
        var list = new List<StoreApp>();

        try
        {
            var shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType is null) return list;

            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic folder = shell.NameSpace("shell:AppsFolder");
            if (folder is null) return list;

            var items = (System.Collections.IEnumerable)folder.Items();
            foreach (var raw in items)
            {
                dynamic item = raw;
                string name = item.Name;
                string path = item.Path;

                if (IsNoise(name, path)) continue;
                list.Add(new StoreApp(name, path));
            }
        }
        catch
        {
            // Enumerating the shell folder is best-effort; the launcher must not fail.
        }

        return list;
    }

    public static IReadOnlyList<Candidate> ToCandidates(IReadOnlyList<StoreApp> apps, IEnumerable<string> existingTitles)
    {
        var titles = new HashSet<string>(existingTitles, StringComparer.OrdinalIgnoreCase);
        var list = new List<Candidate>();

        foreach (var app in apps)
        {
            if (!titles.Add(app.Name)) continue;
            list.Add(ToCandidate(app));
        }

        return list;
    }

    public static Candidate ToCandidate(StoreApp app)
    {
        var target = IsAbsolutePath(app.Path) ? app.Path : "shell:AppsFolder\\" + app.Path;
        return new Candidate($"store:{app.Path}", CandidateKind.OpenApp, app.Name, "Application", app.Name, target);
    }

    public static bool IsNoise(string name, string path)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(path)) return true;

        var lowerName = name.ToLowerInvariant();
        var lowerPath = path.ToLowerInvariant();

        if (lowerName.Contains("uninstall") || lowerName.Contains("desinstalar")) return true;
        if (lowerPath.Contains(@"\unins")) return true;

        foreach (var word in new[] { "documentation", "release notes", "installation notes", "user manual" })
            if (lowerName.Contains(word)) return true;

        if (lowerPath.StartsWith("http", StringComparison.Ordinal)) return true;

        var extension = Path.GetExtension(lowerPath);
        if (extension is ".html" or ".htm" or ".txt" or ".url" or ".pdf") return true;

        return false;
    }

    private static bool IsAbsolutePath(string path) =>
        path.Length >= 3 && char.IsLetter(path[0]) && path[1] == ':' && (path[2] == '\\' || path[2] == '/');
}
