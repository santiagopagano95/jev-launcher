namespace JevLauncher.Core;

public enum CandidateKind
{
    OpenApp,
    OpenFile,
    WebSearch,
    Calculate,
    SystemToggle,
    RunShortcut,
    Command,
    OpenUrl,
    Unclear,
}

public sealed record Candidate(
    string Id,
    CandidateKind Kind,
    string Title,
    string Detail,
    string Keywords,
    string? Target,
    double Fuzzy = 0);

public sealed record ScoredCandidate(
    Candidate Candidate,
    double Score,
    double TargetProbability,
    bool IsTopReady);

public sealed record LauncherContext(
    string FrontmostApp,
    IReadOnlyList<string> RecentApps,
    string ClipboardKind,
    string TimeOfDay,
    string Weekday);

public sealed record Conversation(IReadOnlyList<Candidate> Candidates, LauncherContext Context);
