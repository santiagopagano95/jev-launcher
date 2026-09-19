namespace JevLauncher.Core;

public interface IJevQuery
{
    Task<JevResponse?> QueryAsync(string query, Conversation conversation, CancellationToken ct = default);
}

public sealed class LauncherEngine
{
    private IReadOnlyList<Candidate> _index;
    private readonly IJevQuery _jev;
    private readonly Stats _stats;
    private readonly Func<LauncherContext> _context;
    private readonly LauncherServices _services;
    private readonly List<string> _recentApps = new();

    private int _sequence;
    private int _applied;
    private JevResponse? _last;
    private string _lastQuery = string.Empty;

    public LauncherEngine(IReadOnlyList<Candidate> index, IJevQuery jev, Stats stats, Func<LauncherContext> context,
        LauncherServices? services = null)
    {
        _index = index;
        _jev = jev;
        _stats = stats;
        _context = context;
        _services = services ?? new LauncherServices();
    }

    public Stats Stats => _stats;
    public bool LastWasStale { get; private set; }

    public void SetIndex(IReadOnlyList<Candidate> index) => _index = index;

    private IReadOnlyList<ScoredCandidate> ResolveCommand(string query) =>
        Ranker.Rank(ResolveCommandCandidates(query), null);

    private IReadOnlyList<Candidate> ResolveCommandCandidates(string query)
    {
        var (name, argument) = CommandParser.Split(query);
        if (string.IsNullOrEmpty(name)) return CommandCandidates.Palette(string.Empty);

        var command = CommandParser.Resolve(name);
        if (command is null) return CommandCandidates.Palette(name);

        switch (command.Scope)
        {
            case CommandScope.Web:
            {
                var useDesktopApp = command.Scheme is not null &&
                                    (_services.IsProtocolRegistered ?? Protocols.IsRegistered)(command.Scheme);

                if (!string.IsNullOrWhiteSpace(argument))
                    return new List<Candidate>
                    {
                        useDesktopApp
                            ? CommandCandidates.DesktopSearch(command, argument)
                            : CommandCandidates.Web(command, argument),
                    };

                if (useDesktopApp && !string.IsNullOrWhiteSpace(command.DesktopHome))
                    return new List<Candidate> { CommandCandidates.DesktopHome(command) };

                if (!string.IsNullOrWhiteSpace(command.HomeUrl))
                    return new List<Candidate> { CommandCandidates.Home(command) };

                return new List<Candidate> { CommandCandidates.ToPaletteRow(command) };
            }
            case CommandScope.Files:
                return Prefilter.BuildCandidates(argument, _index, 15, CandidateKind.OpenFile);
            case CommandScope.Apps:
                return Prefilter.BuildCandidates(argument, _index, 15, CandidateKind.OpenApp);
            case CommandScope.Toggles:
                return Prefilter.BuildCandidates(argument, _index, 15, CandidateKind.SystemToggle);
            case CommandScope.Recent:
                return Prefilter.BuildCandidates(argument, _index, 10, CandidateKind.OpenFile);
            case CommandScope.Calculator:
                return Prefilter.BuildCandidates(argument, _index, 3, CandidateKind.Calculate)
                    .Where(c => c.Kind == CandidateKind.Calculate)
                    .ToList();
            case CommandScope.Snippets:
                return SnippetCandidates.Build(_services.Snippets, argument);
            case CommandScope.Clipboard:
                return ClipboardCandidates.Build(_services.Clipboard.Items, argument);
            case CommandScope.Utility:
                return UtilityCommands.Build(command.Id, argument);
            case CommandScope.Windows:
                return FilterWindows(argument);
            case CommandScope.Notes:
                return NotesCandidates(argument);
            case CommandScope.Timer:
                return TimerCandidates(argument);
            case CommandScope.AppAction:
                return new List<Candidate> { CommandCandidates.AppAction(command) };
            default:
                return CommandCandidates.Palette(name);
        }
    }

    private IReadOnlyList<Candidate> FilterWindows(string argument)
    {
        var windows = _services.Windows?.Invoke() ?? Array.Empty<Candidate>();
        if (string.IsNullOrWhiteSpace(argument)) return windows;

        return windows
            .Select(c => (Candidate: c, Score: Fuzzy.Score(argument, c.Title, c.Detail)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Select(x => x.Candidate)
            .ToList();
    }

    private IReadOnlyList<Candidate> NotesCandidates(string argument)
    {
        if (!string.IsNullOrWhiteSpace(argument))
        {
            return new List<Candidate>
            {
                new("note:save", CandidateKind.SaveNote,
                    $"Save note: {SnippetCandidates.Preview(argument)}", "Notes", "note save", argument),
            };
        }

        var recent = _services.Notes?.Recent(10) ?? Array.Empty<string>();
        return recent
            .Select(n => new Candidate("note:" + n.GetHashCode(), CandidateKind.Copy,
                SnippetCandidates.Preview(n), "Note", n, n))
            .ToList();
    }

    private static IReadOnlyList<Candidate> TimerCandidates(string argument)
    {
        var milliseconds = TimerCommand.TryParseMilliseconds(argument);
        return milliseconds is null
            ? new List<Candidate>
            {
                new("timer:hint", CandidateKind.Command, "Timer", "Try /timer 5m", "timer", "/timer "),
            }
            : new List<Candidate>
            {
                new("timer:start", CandidateKind.Timer, $"Timer {argument.Trim()}",
                    "Notifies when it finishes", "timer", milliseconds.Value.ToString()),
            };
    }

    public IReadOnlyList<ScoredCandidate> Update(string query)
    {
        _lastQuery = query;
        if (string.IsNullOrWhiteSpace(query)) return Array.Empty<ScoredCandidate>();
        if (CommandParser.IsCommand(query)) return ResolveCommand(query);
        var candidates = Prefilter.BuildCandidates(query, _index, 15);
        return Ranker.Rank(candidates, _last);
    }

    public async Task<IReadOnlyList<ScoredCandidate>> UpdateAsync(string query)
    {
        _lastQuery = query;
        if (string.IsNullOrWhiteSpace(query)) return Array.Empty<ScoredCandidate>();
        if (CommandParser.IsCommand(query)) return ResolveCommand(query);

        var candidates = Prefilter.BuildCandidates(query, _index, 15);
        var local = Ranker.Rank(candidates, _last);

        var seq = Interlocked.Increment(ref _sequence);
        var conversation = new Conversation(candidates, _context());
        var started = Environment.TickCount64;
        var response = await _jev.QueryAsync(query, conversation).ConfigureAwait(false);
        var elapsed = Environment.TickCount64 - started;

        if (seq < _applied)
        {
            LastWasStale = true;
            return local;
        }

        _applied = seq;
        LastWasStale = false;

        if (response is not null)
        {
            _last = response;
            _stats.Record(elapsed, response.InputTokens);
        }

        if (_lastQuery != query) return local;
        return Ranker.Rank(candidates, _last);
    }
}
