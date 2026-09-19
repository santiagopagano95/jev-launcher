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

        var startMenuApps = StartMenuApps().ToList();
        list.AddRange(startMenuApps);
        list.AddRange(StoreApps.ToCandidates(StoreApps.Enumerate(), startMenuApps.Select(a => a.Title)));
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

    private static readonly HashSet<string> BlockedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".desktop", ".lnk", ".url", ".tmp", ".ini", ".crdownload", ".part",
    };

    /// <summary>Files Windows cannot open (or that only add noise) are not indexed.</summary>
    public static bool IsIndexableFile(string path)
    {
        var name = Path.GetFileName(path);
        if (string.IsNullOrEmpty(name)) return false;
        if (name.StartsWith("~$", StringComparison.Ordinal)) return false;

        return !BlockedExtensions.Contains(Path.GetExtension(name));
    }

    public static Candidate AppCandidateFromShortcut(string fileName, string path)    {
        var title = Path.GetFileNameWithoutExtension(fileName);
        return new Candidate($"app:{path}", CandidateKind.OpenApp, title, "Application", title, path);
    }

    public static IEnumerable<Candidate> Files()
    {
        var entries = new List<(string Path, DateTime Modified)>();
        foreach (var folder in IndexedFolders.Where(Directory.Exists))
            CollectFiles(folder, 0, entries);

        return entries
            .OrderByDescending(e => e.Modified)
            .Select(e => FileCandidateFromPath(e.Path, e.Modified));
    }

    private static void CollectFiles(string folder, int depth, List<(string Path, DateTime Modified)> entries)
    {
        const int cap = 400;
        IEnumerable<string> files;
        IEnumerable<string> dirs;
        try
        {
            files = Directory.EnumerateFiles(folder).Take(cap).ToList();
            dirs = Directory.EnumerateDirectories(folder).Take(cap).ToList();
        }
        catch (UnauthorizedAccessException) { return; }

        foreach (var file in files)
        {
            if (!IsIndexableFile(file)) continue;

            DateTime modified;
            try { modified = File.GetLastWriteTime(file); }
            catch { continue; }
            entries.Add((file, modified));
        }

        if (depth >= 1) return;
        foreach (var dir in dirs)
            CollectFiles(dir, depth + 1, entries);
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
