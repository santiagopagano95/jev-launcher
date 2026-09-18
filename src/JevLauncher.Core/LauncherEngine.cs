namespace JevLauncher.Core;

public interface IJevQuery
{
    Task<JevResponse?> QueryAsync(string query, Conversation conversation, CancellationToken ct = default);
}

public sealed class LauncherEngine
{
    private readonly IReadOnlyList<Candidate> _index;
    private readonly IJevQuery _jev;
    private readonly Stats _stats;
    private readonly Func<LauncherContext> _context;
    private readonly List<string> _recentApps = new();

    private int _sequence;
    private int _applied;
    private JevResponse? _last;
    private string _lastQuery = string.Empty;

    public LauncherEngine(IReadOnlyList<Candidate> index, IJevQuery jev, Stats stats, Func<LauncherContext> context)
    {
        _index = index;
        _jev = jev;
        _stats = stats;
        _context = context;
    }

    public Stats Stats => _stats;
    public bool LastWasStale { get; private set; }

    public IReadOnlyList<ScoredCandidate> Update(string query)
    {
        _lastQuery = query;
        if (string.IsNullOrWhiteSpace(query)) return Array.Empty<ScoredCandidate>();
        var candidates = Prefilter.BuildCandidates(query, _index, 15);
        return Ranker.Rank(candidates, _last);
    }

    public async Task<IReadOnlyList<ScoredCandidate>> UpdateAsync(string query)
    {
        _lastQuery = query;
        if (string.IsNullOrWhiteSpace(query)) return Array.Empty<ScoredCandidate>();

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
