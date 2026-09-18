using System.Text.Json;

namespace JevLauncher.Core;

public sealed record JevResponse(
    string TargetChoice,
    IReadOnlyDictionary<string, double> TargetProbabilities,
    string ActionChoice,
    double Ready,
    int InputTokens,
    int OutputTokens)
{
    public static JevResponse Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var targetChoice = "none";
        var probs = new Dictionary<string, double>();
        var actionChoice = "unclear";
        double ready = 0;
        int input = 0, output = 0;

        if (root.TryGetProperty("answers", out var answers))
        {
            if (answers.TryGetProperty("target", out var target))
            {
                if (target.TryGetProperty("choice", out var ch) && ch.ValueKind == JsonValueKind.String)
                    targetChoice = ch.GetString()!;
                if (target.TryGetProperty("probabilities", out var p) && p.ValueKind == JsonValueKind.Object)
                    foreach (var prop in p.EnumerateObject())
                        probs[prop.Name] = prop.Value.GetDouble();
            }
            if (answers.TryGetProperty("action", out var action) &&
                action.TryGetProperty("choice", out var ach) && ach.ValueKind == JsonValueKind.String)
                actionChoice = ach.GetString()!;

            if (answers.TryGetProperty("ready", out var r) &&
                r.TryGetProperty("noul", out var n) && n.ValueKind == JsonValueKind.Number)
                ready = n.GetDouble();
        }

        if (root.TryGetProperty("usage", out var usage))
        {
            if (usage.TryGetProperty("input_tokens", out var it) && it.ValueKind == JsonValueKind.Number)
                input = it.GetInt32();
            if (usage.TryGetProperty("output_tokens", out var ot) && ot.ValueKind == JsonValueKind.Number)
                output = ot.GetInt32();
        }

        return new JevResponse(targetChoice, probs, actionChoice, ready, input, output);
    }
}
