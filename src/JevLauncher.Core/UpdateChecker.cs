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

public enum UpdateCheckStatus { UpToDate, UpdateAvailable, Failed }

public sealed record UpdateCheckResult(UpdateCheckStatus Status, ReleaseInfo? Release);

public sealed class UpdateChecker
{
    public const string LatestReleaseUrl =
        "https://api.github.com/repos/santiagopagano95/jev-launcher/releases/latest";

    private readonly HttpClient _http;

    public UpdateChecker(HttpClient http) => _http = http;

    public async Task<UpdateCheckResult> CheckAsync(Version current, CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseUrl);
            request.Headers.UserAgent.ParseAdd("JevLauncher");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");

            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);
            if (!response.IsSuccessStatusCode) return new UpdateCheckResult(UpdateCheckStatus.Failed, null);

            var body = await response.Content.ReadAsStringAsync(ct);
            var release = ParseRelease(body);
            if (release is null) return new UpdateCheckResult(UpdateCheckStatus.Failed, null);

            return Normalize(release.Version) > Normalize(current)
                ? new UpdateCheckResult(UpdateCheckStatus.UpdateAvailable, release)
                : new UpdateCheckResult(UpdateCheckStatus.UpToDate, null);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return new UpdateCheckResult(UpdateCheckStatus.Failed, null);
        }
    }

    public static ReleaseInfo? ParseRelease(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;

            var tag = ReadString(root, "tag_name");
            if (tag is null || !TryParseVersion(tag, out var version)) return null;

            var htmlUrl = ReadString(root, "html_url") ?? "";
            var notes = ReadString(root, "body") ?? "";

            var setupName = $"JevLauncher-Setup-{version}.exe";
            var checksumName = setupName + ".sha256";

            string? setupUrl = null;
            string? checksumUrl = null;
            var setupCount = 0;
            var checksumCount = 0;

            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    if (asset.ValueKind != JsonValueKind.Object) continue;

                    var name = ReadString(asset, "name");
                    var url = ReadString(asset, "browser_download_url");
                    if (name is null || url is null) continue;

                    if (string.Equals(name, checksumName, StringComparison.OrdinalIgnoreCase))
                    {
                        checksumUrl = url;
                        checksumCount++;
                    }
                    else if (string.Equals(name, setupName, StringComparison.OrdinalIgnoreCase))
                    {
                        setupUrl = url;
                        setupCount++;
                    }
                }
            }

            if (setupCount != 1 || checksumCount != 1) return null;

            return new ReleaseInfo(tag, version!, setupUrl!, checksumUrl!, htmlUrl, notes);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static bool TryParseVersion(string tag, out Version? version)
    {
        var trimmed = tag.Length > 0 && (tag[0] == 'v' || tag[0] == 'V') ? tag[1..] : tag;
        return Version.TryParse(trimmed, out version);
    }

    private static Version Normalize(Version version)
        => new(version.Major, version.Minor, Math.Max(version.Build, 0), Math.Max(version.Revision, 0));

    private static string? ReadString(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
