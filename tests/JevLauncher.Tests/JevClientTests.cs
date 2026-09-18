using System.Net;
using System.Text;
using JevLauncher.Core;
using Xunit;

public class JevClientTests
{
    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _f;
        public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> f) => _f = f;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(_f(request));
    }

    private const string OkBody = """{ "answers": { "target": { "choice": "c0", "probabilities": { "c0": 1.0 } }, "action": { "choice": "open_app" }, "ready": { "noul": 0.9 } }, "usage": { "input_tokens": 100, "output_tokens": 0 } }""";

    [Fact]
    public async Task Sends_bearer_token_and_returns_response()
    {
        HttpRequestMessage? seen = null;
        var handler = new FakeHandler(req =>
        {
            seen = req;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(OkBody, Encoding.UTF8, "application/json") };
        });
        var client = new JevClient(new HttpClient(handler), "secret");

        var result = await client.QueryAsync("dark", new Conversation(
            new[] { new Candidate("c0", CandidateKind.OpenApp, "Dark", "", "", null) },
            new LauncherContext("Finder", Array.Empty<string>(), "text", "afternoon", "Thursday")));

        Assert.NotNull(result);
        Assert.Equal("Bearer", seen!.Headers.Authorization!.Scheme);
        Assert.Equal("secret", seen.Headers.Authorization.Parameter);
        Assert.Equal("https://api.typesafe.ai/v1/systemone", seen.RequestUri!.ToString());
    }

    [Fact]
    public async Task Returns_null_on_error_status()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var client = new JevClient(new HttpClient(handler), "bad");
        var result = await client.QueryAsync("dark", new Conversation(
            Array.Empty<Candidate>(),
            new LauncherContext("Finder", Array.Empty<string>(), "text", "afternoon", "Thursday")));
        Assert.Null(result);
    }

    [Fact]
    public async Task Returns_null_when_no_key()
    {
        var client = new JevClient(new HttpClient(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))), null);
        var result = await client.QueryAsync("dark", new Conversation(
            Array.Empty<Candidate>(),
            new LauncherContext("Finder", Array.Empty<string>(), "text", "afternoon", "Thursday")));
        Assert.Null(result);
    }
}
