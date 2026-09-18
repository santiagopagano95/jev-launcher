using System.IO;

namespace JevLauncher.Core;

public static class LocalIndex
{
    private static readonly string[] IndexedFolders =
    {
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + @"\Downloads",
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
    };

    public static IReadOnlyList<Candidate> Build()
    {
        var list = new List<Candidate>();
        list.AddRange(SystemToggles.BuildCandidates());
        list.AddRange(StartMenuApps());
        list.AddRange(Files());
        return list;
    }

    public static IEnumerable<Candidate> StartMenuApps()
    {
        var roots = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs"),
        };

        foreach (var root in roots.Where(Directory.Exists))
        {
            foreach (var lnk in Directory.EnumerateFiles(root, "*.lnk", SearchOption.AllDirectories))
                yield return AppCandidateFromShortcut(Path.GetFileName(lnk), lnk);
        }
    }

    public static Candidate AppCandidateFromShortcut(string fileName, string path)
    {
        var title = Path.GetFileNameWithoutExtension(fileName);
        return new Candidate($"app:{path}", CandidateKind.OpenApp, title, "Application", title, path);
    }

    public static IEnumerable<Candidate> Files()
    {
        foreach (var folder in IndexedFolders.Where(Directory.Exists))
        {
            foreach (var file in EnumerateFolder(folder, 0))
                yield return file;
        }
    }

    private static IEnumerable<Candidate> EnumerateFolder(string folder, int depth)
    {
        const int cap = 400;
        IEnumerable<string> files;
        IEnumerable<string> dirs;
        try
        {
            files = Directory.EnumerateFiles(folder).Take(cap).ToList();
            dirs = Directory.EnumerateDirectories(folder).Take(cap).ToList();
        }
        catch (UnauthorizedAccessException) { yield break; }

        foreach (var file in files)
        {
            var modified = File.GetLastWriteTime(file);
            yield return FileCandidateFromPath(file, modified);
        }

        if (depth >= 1) yield break;
        foreach (var dir in dirs)
            foreach (var nested in EnumerateFolder(dir, depth + 1))
                yield return nested;
    }

    public static Candidate FileCandidateFromPath(string path, DateTime modified)
    {
        var title = Path.GetFileName(path);
        var ext = Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
        var folder = Path.GetFileName(Path.GetDirectoryName(path)) ?? string.Empty;
        var detail = string.IsNullOrEmpty(ext)
            ? $"Folder in {folder} · modified {Recency.Describe(modified)}"
            : $"{ext} in {folder} · modified {Recency.Describe(modified)}";
        return new Candidate($"file:{path}", CandidateKind.OpenFile, title, detail, title, path);
    }
}

public static class Recency
{
    public static string Describe(DateTime modified)
    {
        var delta = DateTime.Now - modified;
        if (delta.TotalMinutes < 1) return "just now";
        if (delta.TotalMinutes < 60) return $"{(int)delta.TotalMinutes} min ago";
        if (delta.TotalHours < 24) return $"{(int)delta.TotalHours} h ago";
        if (delta.TotalDays < 30) return $"{(int)delta.TotalDays} d ago";
        return $"{Math.Max(1, (int)(delta.TotalDays / 30))} months ago";
    }
}
