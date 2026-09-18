using System.Net.Http.Headers;
using System.Text;

namespace JevLauncher.Core;

public sealed class JevClient
{
    private const string Endpoint = "https://api.typesafe.ai/v1/systemone";

    private readonly HttpClient _http;
    private readonly string? _apiKey;

    public JevClient(HttpClient http, string? apiKey)
    {
        _http = http;
        _apiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey;
        _http.Timeout = TimeSpan.FromSeconds(5);
    }

    public bool HasKey => _apiKey is not null;

    public async Task<JevResponse?> QueryAsync(string query, Conversation conversation, CancellationToken ct = default)
    {
        if (_apiKey is null) return null;

        try
        {
            var json = JevQuestions.BuildJson(query, conversation);
            using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);
            if (!response.IsSuccessStatusCode) return null;

            var body = await response.Content.ReadAsStringAsync(ct);
            return JevResponse.Parse(body);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return null;
        }
    }
}
