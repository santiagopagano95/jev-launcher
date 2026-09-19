using JevLauncher.Core;
using Xunit;

public class CommandDesktopTests
{
    private sealed class NullJev : IJevQuery
    {
        public Task<JevResponse?> QueryAsync(string query, Conversation conversation, CancellationToken ct = default)
            => Task.FromResult<JevResponse?>(null);
    }

    private static (LauncherEngine Engine, LauncherServices Services) New()
    {
        var services = new LauncherServices();
        var engine = new LauncherEngine(new List<Candidate>(), new NullJev(), new Stats(),
            () => new LauncherContext("x", Array.Empty<string>(), "text", "afternoon", "Friday"), services);
        return (engine, services);
    }

    [Fact]
    public void Search_uses_the_desktop_app_when_the_scheme_is_registered()
    {
        var (engine, services) = New();
        services.IsProtocolRegistered = scheme => scheme == "spotify";

        var rows = engine.Update("/spotify lofi beats");

        Assert.Equal(CandidateKind.OpenUrl, rows[0].Candidate.Kind);
        Assert.Equal("spotify:search:lofi%20beats", rows[0].Candidate.Target);
    }

    [Fact]
    public void Search_falls_back_to_the_web_without_the_scheme()
    {
        var (engine, services) = New();
        services.IsProtocolRegistered = _ => false;

        var rows = engine.Update("/spotify lofi beats");

        Assert.Equal("https://open.spotify.com/search/lofi%20beats", rows[0].Candidate.Target);
    }

    [Fact]
    public void Home_opens_the_desktop_app_when_registered()
    {
        var (engine, services) = New();
        services.IsProtocolRegistered = _ => true;

        var rows = engine.Update("/spotify");

        Assert.Equal("spotify:", rows[0].Candidate.Target);
    }

    [Fact]
    public void Home_falls_back_to_the_site_without_the_scheme()
    {
        var (engine, services) = New();
        services.IsProtocolRegistered = _ => false;

        var rows = engine.Update("/spotify");

        Assert.Equal("https://open.spotify.com", rows[0].Candidate.Target);
    }

    [Fact]
    public void Protocols_helper_knows_http_and_rejects_unknown_schemes()
    {
        Assert.True(Protocols.IsRegistered("http"));
        Assert.False(Protocols.IsRegistered("definitely-not-a-scheme-xyz"));
        Assert.False(Protocols.IsRegistered(""));
    }
}
