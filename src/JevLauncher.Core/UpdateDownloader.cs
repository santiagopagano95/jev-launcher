using System.Net.Http;
using System.Security.Cryptography;

namespace JevLauncher.Core;

public sealed class UpdateDownloader
{
    private readonly HttpClient _http;

    public UpdateDownloader(HttpClient http) => _http = http;

    public async Task<string> DownloadAsync(ReleaseInfo release, string destinationDirectory, CancellationToken ct = default)
    {
        EnsureHttps(release.SetupUrl);
        EnsureHttps(release.ChecksumUrl);

        var expected = await ReadChecksumAsync(release.ChecksumUrl, ct)
            ?? throw new InvalidOperationException("No se pudo leer el checksum del release.");

        var bytes = await GetBytesAsync(release.SetupUrl, ct);
        var actual = Convert.ToHexString(SHA256.HashData(bytes));
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("El checksum del instalador no coincide.");

        Directory.CreateDirectory(destinationDirectory);
        var path = Path.Combine(destinationDirectory, $"JevLauncher-Setup-{release.Version}.exe");
        var temp = path + ".tmp";
        await File.WriteAllBytesAsync(temp, bytes, ct);
        File.Move(temp, path, overwrite: true);
        return path;
    }

    private async Task<byte[]> GetBytesAsync(string url, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("JevLauncher");
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    private async Task<string?> ReadChecksumAsync(string url, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd("JevLauncher");
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);
            if (!response.IsSuccessStatusCode) return null;

            var text = (await response.Content.ReadAsStringAsync(ct)).TrimStart('\uFEFF').Trim();
            var token = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            return token is not null && Convert.FromHexString(token).Length == 32 ? token : null;
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return null;
        }
    }

    private static void EnsureHttps(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("La URL del release no es HTTPS.");
    }
}
