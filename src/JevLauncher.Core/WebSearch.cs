using System.Net.Http;
using System.Text.Json;

namespace JevLauncher.Core;

public sealed record WebResult(string Title, string Url, string Description);

public interface IWebSearch
{
    Task<IReadOnlyList<WebResult>> SearchAsync(string query, CancellationToken ct = default);
}

public static class WebSearchResponse
{
    public static IReadOnlyList<WebResult> Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var list = new List<WebResult>();

        if (!doc.RootElement.TryGetProperty("web", out var web) ||
            !web.TryGetProperty("results", out var results) ||
            results.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var item in results.EnumerateArray())
        {
            var url = GetString(item, "url");
            if (string.IsNullOrWhiteSpace(url)) continue;

            list.Add(new WebResult(
                GetString(item, "title") ?? url,
                url,
                GetString(item, "description") ?? string.Empty));
        }

        return list;
    }

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}

public static class WebResultCandidates
{
    public static IReadOnlyList<Candidate> Build(IReadOnlyList<WebResult> results) =>
        results.Select((result, index) => new Candidate(
            "web:" + index,
            CandidateKind.OpenUrl,
            result.Title,
            Detail(result),
            result.Title + " " + Domain(result.Url),
            result.Url)).ToList();

    private static string Detail(WebResult result)
    {
        var domain = Domain(result.Url);
        var snippet = Truncate(result.Description.ReplaceLineEndings(" "), 90);
        return string.IsNullOrWhiteSpace(snippet) ? domain : $"{domain} · {snippet}";
    }

    private static string Domain(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : url;

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";
}

public sealed class BraveWebSearch : IWebSearch{
    private const string Endpoint = "https://api.search.brave.com/res/v1/web/search";

    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly int _count;

    public BraveWebSearch(HttpClient http, string apiKey, int count = 5)
    {
        _http = http;
        _apiKey = apiKey;
        _count = Math.Clamp(count, 1, 20);
        _http.Timeout = TimeSpan.FromSeconds(6);
    }

    public async Task<IReadOnlyList<WebResult>> SearchAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey) || string.IsNullOrWhiteSpace(query))
            return Array.Empty<WebResult>();

        try
        {
            var url = $"{Endpoint}?q={Uri.EscapeDataString(query)}&count={_count}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("X-Subscription-Token", _apiKey);
            request.Headers.Add("Accept", "application/json");

            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);
            if (!response.IsSuccessStatusCode) return Array.Empty<WebResult>();

            var body = await response.Content.ReadAsStringAsync(ct);
            return WebSearchResponse.Parse(body);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return Array.Empty<WebResult>();
        }
    }
}
