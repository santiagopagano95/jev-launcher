namespace JevLauncher.Core;

public static class Ranker
{
    private const double TargetWeight = 0.65;
    private const double ActionWeight = 0.20;
    private const double FuzzyWeight = 0.15;
    private const double UsageWeight = 0.12;

    /// <summary>An exact local match must not be overturned by Jev.</summary>
    private const double ExactGuard = 0.75;
    private const double PrefixGuard = 0.55;

    public static IReadOnlyList<ScoredCandidate> Rank(IReadOnlyList<Candidate> candidates, JevResponse? response,
        UsageStats? usage = null)
    {
        var maxFuzzy = candidates.Count == 0 ? 1 : Math.Max(1, candidates.Max(c => c.Fuzzy));

        var scored = candidates.Select(c =>
        {
            var pTarget = response is not null && response.TargetProbabilities.TryGetValue(c.Id, out var p) ? p : 0;
            var actionMatch = response is not null &&
                              string.Equals(JevQuestions.KindName(c.Kind), response.ActionChoice, StringComparison.Ordinal)
                ? 1.0 : 0.0;
            var fuzzyNorm = c.Fuzzy / maxFuzzy;

            var score = response is null
                ? fuzzyNorm
                : TargetWeight * pTarget + ActionWeight * actionMatch + FuzzyWeight * fuzzyNorm;

            score += UsageWeight * (usage?.Boost(c.Id) ?? 0);

            var guard = c.Fuzzy >= 100 ? ExactGuard : c.Fuzzy >= 80 ? PrefixGuard : 0;
            if (guard > score) score = guard;

            return new ScoredCandidate(c, score, pTarget, false);
        })
        .OrderByDescending(s => s.Score)
        .ToList();

        if (scored.Count > 0 && response is not null)
        {
            var top = scored[0];
            var ready = response.Ready >= 0.6 || top.TargetProbability >= 0.9;
            scored[0] = top with { IsTopReady = ready };
        }

        return scored;
    }
}
