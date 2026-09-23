using System.Net.Http;
using System.Text.Json;

namespace JevLauncher.Core;

public sealed record ReleaseInfo(
    string Tag,
    Version Version,
    string SetupUrl,
    string ChecksumUrl,
    string HtmlUrl,
    string Notes);

public sealed class UpdateChecker
{
    public const string LatestReleaseUrl =
        "https://api.github.com/repos/santiagopagano95/jev-launcher/releases/latest";

    private readonly HttpClient _http;

    public UpdateChecker(HttpClient http) => _http = http;

    public async Task<ReleaseInfo?> CheckAsync(Version current, CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseUrl);
            request.Headers.UserAgent.ParseAdd("JevLauncher");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");

            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);
            if (!response.IsSuccessStatusCode) return null;

            var body = await response.Content.ReadAsStringAsync(ct);
            var release = ParseRelease(body);
            return release is not null && release.Version > current ? release : null;
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return null;
        }
    }

    public static ReleaseInfo? ParseRelease(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tag = root.TryGetProperty("tag_name", out var tagElement) ? tagElement.GetString() ?? "" : "";
            if (!TryParseVersion(tag, out var version)) return null;

            var htmlUrl = root.TryGetProperty("html_url", out var html) ? html.GetString() ?? "" : "";
            var notes = root.TryGetProperty("body", out var body) ? body.GetString() ?? "" : "";

            string? setupUrl = null;
            string? checksumUrl = null;

            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    var url = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() ?? "" : "";
                    if (name.StartsWith("JevLauncher-Setup-", StringComparison.OrdinalIgnoreCase) &&
                        name.EndsWith(".exe.sha256", StringComparison.OrdinalIgnoreCase))
                        checksumUrl = url;
                    else if (name.StartsWith("JevLauncher-Setup-", StringComparison.OrdinalIgnoreCase) &&
                             name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        setupUrl = url;
                }
            }

            if (string.IsNullOrEmpty(setupUrl) || string.IsNullOrEmpty(checksumUrl)) return null;

            return new ReleaseInfo(tag, version!, setupUrl, checksumUrl, htmlUrl, notes);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static bool TryParseVersion(string tag, out Version? version)
        => Version.TryParse(tag.TrimStart('v', 'V'), out version);
}
