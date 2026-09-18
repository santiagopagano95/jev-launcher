using JevLauncher.Core;
using Xunit;

public class JevResponseTests
{
    private const string Json = """
    {
      "answers": {
        "target": { "type": "choice", "choice": "c1", "probabilities": { "c0": 0.02, "c1": 0.98, "none": 0.0 }, "confidence": 0.8 },
        "action": { "type": "choice", "choice": "open_file", "probabilities": { "open_file": 0.9, "web_search": 0.1 }, "confidence": 0.7 },
        "ready": { "type": "noul", "noul": 0.4 }
      },
      "usage": { "input_tokens": 1400, "output_tokens": 0 }
    }
    """;

    [Fact]
    public void Parses_target_distribution()
    {
        var r = JevResponse.Parse(Json);
        Assert.Equal("c1", r.TargetChoice);
        Assert.Equal(0.98, r.TargetProbabilities["c1"], 3);
        Assert.Equal(0.4, r.Ready, 3);
        Assert.Equal("open_file", r.ActionChoice);
        Assert.Equal(1400, r.InputTokens);
    }

    [Fact]
    public void Missing_answers_yields_safe_defaults()
    {
        var r = JevResponse.Parse("{}");
        Assert.Equal("none", r.TargetChoice);
        Assert.Empty(r.TargetProbabilities);
        Assert.Equal(0, r.Ready);
    }
}
