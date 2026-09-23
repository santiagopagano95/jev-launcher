using System.Net;
using System.Security.Cryptography;
using System.Text;
using JevLauncher.Core;
using Xunit;

public class UpdateDownloaderTests
{
    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _f;
        public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> f) => _f = f;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(_f(request));
    }

    private static ReleaseInfo Release() => new(
        "v1.1.0", new Version(1, 1, 0),
        "https://example/setup",
        "https://example/sum",
        "https://example/html",
        "notas");

    private static (UpdateDownloader downloader, string dir) Make(byte[] payload, string checksum)
    {
        var handler = new FakeHandler(req => req.RequestUri!.ToString() switch
        {
            "https://example/setup" => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(payload) },
            "https://example/sum" => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(checksum) },
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        });
        var dir = Path.Combine(Path.GetTempPath(), "jev-update-test-" + Guid.NewGuid().ToString("N"));
        return (new UpdateDownloader(new HttpClient(handler)), dir);
    }

    private static string GoodHash(byte[] payload)
        => Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();

    [Fact]
    public async Task Downloads_and_verifies_checksum()
    {
        var payload = Encoding.UTF8.GetBytes("fake installer bytes");
        var (downloader, dir) = Make(payload, $"{GoodHash(payload)}  setup.exe");
        try
        {
            var path = await downloader.DownloadAsync(Release(), dir);
            Assert.True(File.Exists(path));
            Assert.Equal(payload, await File.ReadAllBytesAsync(path));
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task Throws_and_writes_nothing_on_bad_checksum()
    {
        var payload = Encoding.UTF8.GetBytes("fake installer bytes");
        var (downloader, dir) = Make(payload, new string('0', 64));
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => downloader.DownloadAsync(Release(), dir));
            Assert.False(Directory.Exists(dir) && Directory.GetFiles(dir).Length > 0);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task Throws_when_checksum_body_invalid()
    {
        var payload = Encoding.UTF8.GetBytes("fake installer bytes");
        var (downloader, dir) = Make(payload, "not-a-hash");
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => downloader.DownloadAsync(Release(), dir));
            Assert.False(Directory.Exists(dir) && Directory.GetFiles(dir).Length > 0);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }
}
