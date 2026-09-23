using System.Net.Http;
using System.Security.Cryptography;

namespace JevLauncher.Core;

public sealed class UpdateDownloader
{
    private readonly HttpClient _http;

    public UpdateDownloader(HttpClient http) => _http = http;

    public async Task<string> DownloadAsync(ReleaseInfo release, string destinationDirectory, CancellationToken ct = default)
    {
        var expected = await ReadChecksumAsync(release.ChecksumUrl, ct)
            ?? throw new InvalidOperationException("No se pudo leer el checksum del release.");

        var bytes = await _http.GetByteArrayAsync(release.SetupUrl, ct);
        var actual = Convert.ToHexString(SHA256.HashData(bytes));
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("El checksum del instalador no coincide.");

        Directory.CreateDirectory(destinationDirectory);
        var path = Path.Combine(destinationDirectory, $"JevLauncher-Setup-{release.Version}.exe");
        await File.WriteAllBytesAsync(path, bytes, ct);
        return path;
    }

    private async Task<string?> ReadChecksumAsync(string url, CancellationToken ct)
    {
        try
        {
            var text = await _http.GetStringAsync(url, ct);
            var token = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            return token is { Length: 64 } ? token : null;
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return null;
        }
    }
}
