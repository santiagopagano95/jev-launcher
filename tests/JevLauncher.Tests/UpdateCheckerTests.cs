using JevLauncher.Core;
using Xunit;

public class UpdateCheckerTests
{
    private const string ReleaseJson = """
    {
      "tag_name": "v1.1.0",
      "html_url": "https://github.com/santiagopagano95/jev-launcher/releases/tag/v1.1.0",
      "body": "Notas",
      "assets": [
        { "name": "JevLauncher-1.1.0.zip", "browser_download_url": "https://example/zip" },
        { "name": "JevLauncher-Setup-1.1.0.exe", "browser_download_url": "https://example/setup" },
        { "name": "JevLauncher-Setup-1.1.0.exe.sha256", "browser_download_url": "https://example/sum" }
      ]
    }
    """;

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _f;
        public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> f) => _f = f;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(_f(request));
    }

    private static UpdateChecker Checker(Func<HttpRequestMessage, HttpResponseMessage> respond)
        => new(new HttpClient(new FakeHandler(respond)));

    private static HttpResponseMessage Json(string body)
        => new(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
        };

    [Fact]
    public void Parses_tag_version_and_assets()
    {
        var release = UpdateChecker.ParseRelease(ReleaseJson);

        Assert.NotNull(release);
        Assert.Equal("v1.1.0", release!.Tag);
        Assert.Equal(new Version(1, 1, 0), release.Version);
        Assert.Equal("https://example/setup", release.SetupUrl);
        Assert.Equal("https://example/sum", release.ChecksumUrl);
        Assert.Equal("Notas", release.Notes);
    }

    [Fact]
    public void Returns_null_when_checksum_asset_missing()
    {
        var json = ReleaseJson.Replace("JevLauncher-Setup-1.1.0.exe.sha256", "JevLauncher-Setup-1.1.0.exe.sha256.bak");
        Assert.Null(UpdateChecker.ParseRelease(json));
    }

    [Fact]
    public void Returns_null_when_assets_empty()
    {
        Assert.Null(UpdateChecker.ParseRelease("""{ "tag_name": "v1.1.0", "assets": [] }"""));
    }

    [Fact]
    public void Returns_null_when_multiple_setup_assets()
    {
        var json = """
        {
          "tag_name": "v1.1.0",
          "assets": [
            { "name": "JevLauncher-Setup-1.1.0.exe", "browser_download_url": "https://example/a" },
            { "name": "JevLauncher-Setup-1.1.0.exe", "browser_download_url": "https://example/b" },
            { "name": "JevLauncher-Setup-1.1.0.exe.sha256", "browser_download_url": "https://example/sum" }
          ]
        }
        """;
        Assert.Null(UpdateChecker.ParseRelease(json));
    }

    [Fact]
    public void Returns_null_on_invalid_json()
    {
        Assert.Null(UpdateChecker.ParseRelease("{ not json"));
    }

    [Fact]
    public void Returns_null_on_numeric_tag()
    {
        Assert.Null(UpdateChecker.ParseRelease("""{ "tag_name": 123, "assets": [] }"""));
    }

    [Fact]
    public void Returns_null_on_non_object_asset()
    {
        Assert.Null(UpdateChecker.ParseRelease("""{ "tag_name": "v1.1.0", "assets": [ "oops" ] }"""));
    }

    [Theory]
    [InlineData("v1.1.0", 1, 1, 0)]
    [InlineData("1.2.3", 1, 2, 3)]
    [InlineData("v2.0", 2, 0, -1)]
    public void Parses_tag_versions(string tag, int major, int minor, int build)
    {
        Assert.True(UpdateChecker.TryParseVersion(tag, out var version));
        var expected = build < 0 ? new Version(major, minor) : new Version(major, minor, build);
        Assert.Equal(expected, version);
    }

    [Fact]
    public async Task Reports_update_when_newer()
    {
        var checker = Checker(_ => Json(ReleaseJson));

        var result = await checker.CheckAsync(new Version(1, 0, 0));

        Assert.Equal(UpdateCheckStatus.UpdateAvailable, result.Status);
        Assert.Equal(new Version(1, 1, 0), result.Release!.Version);
    }

    [Fact]
    public async Task Reports_up_to_date_when_current_or_newer()
    {
        var checker = Checker(_ => Json(ReleaseJson));

        Assert.Equal(UpdateCheckStatus.UpToDate, (await checker.CheckAsync(new Version(1, 1, 0))).Status);
        Assert.Equal(UpdateCheckStatus.UpToDate, (await checker.CheckAsync(new Version(2, 0, 0))).Status);
    }

    [Fact]
    public async Task Reports_failed_on_http_error()
    {
        var checker = Checker(_ => new HttpResponseMessage(System.Net.HttpStatusCode.Forbidden));

        Assert.Equal(UpdateCheckStatus.Failed, (await checker.CheckAsync(new Version(1, 0, 0))).Status);
    }

    [Fact]
    public async Task Reports_failed_on_invalid_body()
    {
        var checker = Checker(_ => Json("{ not json"));

        Assert.Equal(UpdateCheckStatus.Failed, (await checker.CheckAsync(new Version(1, 0, 0))).Status);
    }
}
