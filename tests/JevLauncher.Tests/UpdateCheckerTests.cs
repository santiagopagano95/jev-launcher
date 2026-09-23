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
    public void Returns_null_on_invalid_json()
    {
        Assert.Null(UpdateChecker.ParseRelease("{ not json"));
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
}
