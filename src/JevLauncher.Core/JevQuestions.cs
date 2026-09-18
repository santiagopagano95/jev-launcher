using System.Text.Json;
using System.Text.Json.Nodes;

namespace JevLauncher.Core;

public static class JevQuestions
{
    public const string QueryNote =
        "Text the user has typed so far into a Spotlight-style Windows launcher. " +
        "It is often an incomplete prefix or a short natural-language phrase.";

    public static string BuildJson(string query, Conversation conv)
    {
        var candidates = new JsonArray();
        var targetCriteria = new JsonObject();

        for (var i = 0; i < conv.Candidates.Count; i++)
        {
            var c = conv.Candidates[i];
            candidates.Add(new JsonObject
            {
                ["id"] = c.Id,
                ["kind"] = KindName(c.Kind),
                ["title"] = c.Title,
                ["detail"] = c.Detail,
            });
            targetCriteria[c.Id] = $"{c.Title} — {c.Detail}";
        }
        targetCriteria["none"] = "No candidate is the intended target.";

        var context = new JsonObject
        {
            ["frontmost_app"] = conv.Context.FrontmostApp,
            ["recent_apps"] = new JsonArray(conv.Context.RecentApps.Select(a => (JsonNode)a!).ToArray()),
            ["clipboard_kind"] = conv.Context.ClipboardKind,
            ["time_of_day"] = conv.Context.TimeOfDay,
            ["weekday"] = conv.Context.Weekday,
        };

        var state = new JsonObject
        {
            ["query"] = query,
            ["query_note"] = QueryNote,
            ["context"] = context,
            ["candidates"] = candidates,
        };

        var questions = new JsonObject
        {
            ["target"] = new JsonObject
            {
                ["type"] = "choice",
                ["instructions"] =
                    "Which entry in candidates is the item they intend to open or run? " +
                    "Treat query as a possibly incomplete prefix or paraphrase. Match on meaning. " +
                    "detail carries recency: prefer the entry whose detail says it was modified most recently. " +
                    "candidates is the complete option set; use none only if nothing fits.",
                ["criteria"] = targetCriteria,
            },
            ["action"] = new JsonObject
            {
                ["type"] = "choice",
                ["instructions"] = "What action does query intend?",
                ["criteria"] = new JsonObject
                {
                    ["open_app"] = "Launch an installed application.",
                    ["open_file"] = "Open a local file or folder.",
                    ["web_search"] = "Search the web; only a fallback when nothing local fits.",
                    ["calculate"] = "Evaluate arithmetic.",
                    ["system_toggle"] = "Flip a system setting (dark mode, wifi, sleep, mute…).",
                    ["run_shortcut"] = "Run a named shortcut or script.",
                    ["unclear"] = "The intent is not clear.",
                },
            },
            ["ready"] = new JsonObject
            {
                ["type"] = "noul",
                ["instructions"] =
                    "The launcher is about to run the best-matching candidate the instant the user presses Enter. " +
                    "candidates is the complete option set and web_search is only a fallback. " +
                    "A short prefix can already be unambiguous (for example 'dark'). " +
                    "Is query already unambiguous enough to run the top candidate?",
            },
        };

        var root = new JsonObject
        {
            ["model"] = "jev-latest",
            ["state"] = state,
            ["questions"] = questions,
        };

        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    public static string KindName(CandidateKind kind) => kind switch
    {
        CandidateKind.OpenApp => "open_app",
        CandidateKind.OpenFile => "open_file",
        CandidateKind.WebSearch => "web_search",
        CandidateKind.Calculate => "calculate",
        CandidateKind.SystemToggle => "system_toggle",
        CandidateKind.RunShortcut => "run_shortcut",
        _ => "unclear",
    };
}
