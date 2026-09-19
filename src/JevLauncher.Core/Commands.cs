namespace JevLauncher.Core;

public enum CommandScope
{
    Web,
    Files,
    Apps,
    Toggles,
    Recent,
    Calculator,
    Snippets,
    Clipboard,
    Utility,
    Windows,
    Notes,
    Timer,
    AppAction,
}

public sealed record LauncherCommand(
    string Id,
    string Title,
    string Description,
    IReadOnlyList<string> Aliases,
    CommandScope Scope,
    string? UrlTemplate = null,
    string? HomeUrl = null)
{
    public string InsertText => "/" + Id + " ";

    public string Keywords => string.Join(' ', new[] { Id, Title, Description }.Concat(Aliases));
}

public static class Commands
{
    public static readonly IReadOnlyList<LauncherCommand> All = new[]
    {
        // Web search
        new LauncherCommand("web", "Search the web", "Google", new[] { "g", "search", "s" },
            CommandScope.Web, "https://www.google.com/search?q={0}"),
        new LauncherCommand("ddg", "DuckDuckGo", "Search the web", Array.Empty<string>(), CommandScope.Web,
            "https://duckduckgo.com/?q={0}", "https://duckduckgo.com"),
        new LauncherCommand("bing", "Bing", "Search the web", Array.Empty<string>(), CommandScope.Web,
            "https://www.bing.com/search?q={0}", "https://www.bing.com"),
        new LauncherCommand("yt", "YouTube", "Search videos", Array.Empty<string>(), CommandScope.Web,
            "https://www.youtube.com/results?search_query={0}", "https://www.youtube.com"),
        new LauncherCommand("gh", "GitHub", "Search repositories and code", Array.Empty<string>(), CommandScope.Web,
            "https://github.com/search?q={0}", "https://github.com"),
        new LauncherCommand("wiki", "Wikipedia", "Search articles", new[] { "w" }, CommandScope.Web,
            "https://en.wikipedia.org/w/index.php?search={0}", "https://en.wikipedia.org"),
        new LauncherCommand("so", "Stack Overflow", "Search questions", new[] { "stack" }, CommandScope.Web,
            "https://stackoverflow.com/search?q={0}", "https://stackoverflow.com"),
        new LauncherCommand("maps", "Google Maps", "Search places", new[] { "m" }, CommandScope.Web,
            "https://www.google.com/maps/search/{0}", "https://maps.google.com"),
        new LauncherCommand("img", "Google Images", "Search images", new[] { "i" }, CommandScope.Web,
            "https://www.google.com/search?tbm=isch&q={0}", "https://images.google.com"),

        // Social
        new LauncherCommand("x", "X", "Search posts", new[] { "tw", "twitter" }, CommandScope.Web,
            "https://x.com/search?q={0}", "https://x.com"),
        new LauncherCommand("reddit", "Reddit", "Search posts", new[] { "r" }, CommandScope.Web,
            "https://www.reddit.com/search/?q={0}", "https://www.reddit.com"),
        new LauncherCommand("ig", "Instagram", "Search", new[] { "instagram" }, CommandScope.Web,
            "https://www.instagram.com/explore/search/keyword/?q={0}", "https://www.instagram.com"),
        new LauncherCommand("tiktok", "TikTok", "Search videos", new[] { "tt" }, CommandScope.Web,
            "https://www.tiktok.com/search?q={0}", "https://www.tiktok.com"),
        new LauncherCommand("li", "LinkedIn", "Search people and jobs", new[] { "linkedin" }, CommandScope.Web,
            "https://www.linkedin.com/search/results/all/?keywords={0}", "https://www.linkedin.com/feed"),
        new LauncherCommand("fb", "Facebook", "Search", new[] { "facebook" }, CommandScope.Web,
            "https://www.facebook.com/search/top?q={0}", "https://www.facebook.com"),
        new LauncherCommand("bsky", "Bluesky", "Search posts", new[] { "bluesky" }, CommandScope.Web,
            "https://bsky.app/search?q={0}", "https://bsky.app"),
        new LauncherCommand("hn", "Hacker News", "Search stories", Array.Empty<string>(), CommandScope.Web,
            "https://hn.algolia.com/?q={0}", "https://news.ycombinator.com"),

        // Streaming and services
        new LauncherCommand("spotify", "Spotify", "Search music", Array.Empty<string>(), CommandScope.Web,
            "https://open.spotify.com/search/{0}", "https://open.spotify.com"),
        new LauncherCommand("netflix", "Netflix", "Search shows", Array.Empty<string>(), CommandScope.Web,
            "https://www.netflix.com/search?q={0}", "https://www.netflix.com"),
        new LauncherCommand("ytmusic", "YouTube Music", "Search music", new[] { "music" }, CommandScope.Web,
            "https://music.youtube.com/search?q={0}", "https://music.youtube.com"),
        new LauncherCommand("twitch", "Twitch", "Search streams", Array.Empty<string>(), CommandScope.Web,
            "https://www.twitch.tv/search?term={0}", "https://www.twitch.tv"),
        new LauncherCommand("prime", "Prime Video", "Search shows", new[] { "primevideo" }, CommandScope.Web,
            "https://www.primevideo.com/search?phrase={0}", "https://www.primevideo.com"),
        new LauncherCommand("disney", "Disney+", "Search shows", new[] { "disneyplus" }, CommandScope.Web,
            "https://www.disneyplus.com/search?q={0}", "https://www.disneyplus.com"),

        // Local scopes
        new LauncherCommand("file", "Find files", "Only local files", Array.Empty<string>(), CommandScope.Files),
        new LauncherCommand("app", "Find apps", "Only installed apps", Array.Empty<string>(), CommandScope.Apps),
        new LauncherCommand("toggle", "System toggles", "Dark mode, wifi, sleep…", new[] { "toggles" }, CommandScope.Toggles),
        new LauncherCommand("recent", "Recent files", "Newest local files", Array.Empty<string>(), CommandScope.Recent),
        new LauncherCommand("calc", "Calculator", "Evaluate arithmetic", Array.Empty<string>(), CommandScope.Calculator),

        // Utilities
        new LauncherCommand("snip", "Snippets", "Reusable text you defined", Array.Empty<string>(), CommandScope.Snippets),
        new LauncherCommand("clip", "Clipboard history", "Recently copied text", new[] { "clipboard" }, CommandScope.Clipboard),
        new LauncherCommand("uuid", "UUID", "Generate a UUID and copy it", Array.Empty<string>(), CommandScope.Utility),
        new LauncherCommand("b64", "Base64 encode", "Encode text as Base64", new[] { "base64" }, CommandScope.Utility),
        new LauncherCommand("b64d", "Base64 decode", "Decode Base64 text", Array.Empty<string>(), CommandScope.Utility),
        new LauncherCommand("hash", "SHA-256", "Hash text and copy it", new[] { "sha256" }, CommandScope.Utility),
        new LauncherCommand("color", "Color", "Normalize a hex color", Array.Empty<string>(), CommandScope.Utility),
        new LauncherCommand("win", "Windows", "Switch to an open window", new[] { "window" }, CommandScope.Windows),
        new LauncherCommand("note", "Notes", "Save or read quick notes", Array.Empty<string>(), CommandScope.Notes),
        new LauncherCommand("timer", "Timer", "Example: /timer 5m", Array.Empty<string>(), CommandScope.Timer),

        // App actions
        new LauncherCommand("settings", "Settings", "API key, hotkey, search template", Array.Empty<string>(), CommandScope.AppAction),
        new LauncherCommand("quit", "Quit Launcher", "Exit Jev Launcher", Array.Empty<string>(), CommandScope.AppAction),
        new LauncherCommand("help", "Help", "List every command", new[] { "?" }, CommandScope.AppAction),
    };
}

public static class CommandParser
{
    public static bool IsCommand(string? query) => (query ?? string.Empty).TrimStart().StartsWith('/');

    public static (string Name, string Argument) Split(string query)
    {
        var text = (query ?? string.Empty).TrimStart();
        if (text.StartsWith('/')) text = text[1..];

        var space = text.IndexOf(' ');
        return space < 0 ? (text, string.Empty) : (text[..space], text[(space + 1)..].Trim());
    }

    public static LauncherCommand? Resolve(string name) =>
        string.IsNullOrEmpty(name)
            ? null
            : Commands.All.FirstOrDefault(c =>
                c.Id.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                c.Aliases.Any(a => a.Equals(name, StringComparison.OrdinalIgnoreCase)));
}
