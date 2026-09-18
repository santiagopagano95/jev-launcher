namespace JevLauncher.Core;

public enum CommandScope
{
    Web,
    Files,
    Apps,
    Toggles,
    Recent,
    Calculator,
    AppAction,
}

public sealed record LauncherCommand(
    string Id,
    string Title,
    string Description,
    IReadOnlyList<string> Aliases,
    CommandScope Scope,
    string? UrlTemplate = null)
{
    public string InsertText => "/" + Id + " ";

    public string Keywords => string.Join(' ', new[] { Id, Title, Description }.Concat(Aliases));
}

public static class Commands
{
    public static readonly IReadOnlyList<LauncherCommand> All = new[]
    {
        new LauncherCommand("web", "Search the web", "Google", new[] { "g" }, CommandScope.Web,
            "https://www.google.com/search?q={0}"),
        new LauncherCommand("ddg", "DuckDuckGo", "Search the web", Array.Empty<string>(), CommandScope.Web,
            "https://duckduckgo.com/?q={0}"),
        new LauncherCommand("bing", "Bing", "Search the web", Array.Empty<string>(), CommandScope.Web,
            "https://www.bing.com/search?q={0}"),
        new LauncherCommand("yt", "YouTube", "Search videos", Array.Empty<string>(), CommandScope.Web,
            "https://www.youtube.com/results?search_query={0}"),
        new LauncherCommand("gh", "GitHub", "Search repositories and code", Array.Empty<string>(), CommandScope.Web,
            "https://github.com/search?q={0}"),
        new LauncherCommand("wiki", "Wikipedia", "Search articles", new[] { "w" }, CommandScope.Web,
            "https://en.wikipedia.org/w/index.php?search={0}"),
        new LauncherCommand("so", "Stack Overflow", "Search questions", new[] { "stack" }, CommandScope.Web,
            "https://stackoverflow.com/search?q={0}"),
        new LauncherCommand("maps", "Google Maps", "Search places", new[] { "m" }, CommandScope.Web,
            "https://www.google.com/maps/search/{0}"),
        new LauncherCommand("img", "Google Images", "Search images", new[] { "i" }, CommandScope.Web,
            "https://www.google.com/search?tbm=isch&q={0}"),
        new LauncherCommand("file", "Find files", "Only local files", Array.Empty<string>(), CommandScope.Files),
        new LauncherCommand("app", "Find apps", "Only installed apps", Array.Empty<string>(), CommandScope.Apps),
        new LauncherCommand("toggle", "System toggles", "Dark mode, wifi, sleep…", new[] { "toggles" }, CommandScope.Toggles),
        new LauncherCommand("recent", "Recent files", "Newest local files", Array.Empty<string>(), CommandScope.Recent),
        new LauncherCommand("calc", "Calculator", "Evaluate arithmetic", Array.Empty<string>(), CommandScope.Calculator),
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
