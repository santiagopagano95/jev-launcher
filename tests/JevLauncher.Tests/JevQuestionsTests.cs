using System.Text.Json;
using JevLauncher.Core;
using Xunit;

public class JevQuestionsTests
{
    private static Conversation Sample() => new(
        new List<Candidate>
        {
            new("c0", CandidateKind.SystemToggle, "Toggle Dark Mode", "System toggle", "dark appearance theme", "dark-mode"),
            new("c1", CandidateKind.OpenFile, "Q3-Roadmap-Review.pdf", "PDF in ~/Downloads · modified 16 min ago", "pdf", @"C:\q3.pdf"),
            new("c2", CandidateKind.WebSearch, "Search the web for “the pdf I”", "Opens your default browser", "web", "the pdf I"),
        },
        new LauncherContext("Finder", new[] { "Finder", "Safari" }, "text", "afternoon", "Thursday"));

    [Fact]
    public void Builds_three_questions_with_ids()
    {
        var json = JevQuestions.BuildJson("the pdf I", Sample());
        using var doc = JsonDocument.Parse(json);
        var questions = doc.RootElement.GetProperty("questions");
        Assert.Equal("choice", questions.GetProperty("target").GetProperty("type").GetString());
        Assert.Equal("choice", questions.GetProperty("action").GetProperty("type").GetString());
        Assert.Equal("noul", questions.GetProperty("ready").GetProperty("type").GetString());

        var criteria = questions.GetProperty("target").GetProperty("criteria");
        Assert.True(criteria.TryGetProperty("c0", out _));
        Assert.True(criteria.TryGetProperty("c1", out _));
        Assert.True(criteria.TryGetProperty("none", out _));
    }

    [Fact]
    public void State_includes_query_and_context()
    {
        var json = JevQuestions.BuildJson("dark", Sample());
        using var doc = JsonDocument.Parse(json);
        var state = doc.RootElement.GetProperty("state");
        Assert.Equal("dark", state.GetProperty("query").GetString());
        Assert.Equal("Finder", state.GetProperty("context").GetProperty("frontmost_app").GetString());
        Assert.Equal("jev-latest", doc.RootElement.GetProperty("model").GetString());
    }

    [Fact]
    public void Candidate_ids_map_back_to_source()
    {
        var conv = Sample();
        var json = JevQuestions.BuildJson("pdf", conv);
        Assert.Equal("c0", conv.Candidates[0].Id);
        Assert.Contains("c0", json);
    }
}
