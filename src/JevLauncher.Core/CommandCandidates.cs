namespace JevLauncher.Core;

public static class CommandCandidates
{
    public static IReadOnlyList<Candidate> Palette(string name)
    {
        IEnumerable<LauncherCommand> matches = Commands.All;

        if (!string.IsNullOrWhiteSpace(name))
        {
            matches = Commands.All
                .Select(c => (Command: c, Score: Fuzzy.Score(name, c.Title, c.Keywords)))
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .Select(x => x.Command);
        }

        return matches.Select(ToPaletteRow).ToList();
    }

    public static Candidate ToPaletteRow(LauncherCommand command) =>
        new("cmd:" + command.Id, CandidateKind.Command, command.Title, command.Description,
            command.Keywords, command.InsertText);

    public static Candidate Web(LauncherCommand command, string argument) =>
        new("url:" + command.Id, CandidateKind.OpenUrl,
            $"Search {command.Title} for “{argument}”", "Opens your default browser",
            command.Keywords, BuildUrl(command.UrlTemplate!, argument));

    public static Candidate AppAction(LauncherCommand command) =>
        new("app:" + command.Id, CandidateKind.Command, command.Title, command.Description,
            command.Keywords, command.InsertText);

    public static string BuildUrl(string template, string argument) =>
        string.Format(template, Uri.EscapeDataString(argument));
}
