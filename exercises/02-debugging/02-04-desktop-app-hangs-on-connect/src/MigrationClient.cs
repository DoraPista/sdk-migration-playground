using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace MigrationKit.Client;

public sealed class MigrationClient
{
    private readonly HttpClient _http;
    private readonly string _clientId;
    private readonly string _clientSecret;

    public MigrationClient(HttpClient http, string clientId, string clientSecret)
    {
        _http = http;
        _clientId = clientId;
        _clientSecret = clientSecret;
    }

    public bool IsConnected { get; private set; }

    public string? ServiceStatus { get; private set; }

    // ------------------------------------------------------------------
    // Synchronous API: used by the WPF application's button handlers.
    // ------------------------------------------------------------------

    public void Connect() => ConnectAsync().Wait();

    public bool CheckHealth() => CheckHealthAsync().Result;

    // ------------------------------------------------------------------
    // Asynchronous API
    // ------------------------------------------------------------------

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = _clientId,
            ["client_secret"] = _clientSecret,
        });

        using var response = await _http.PostAsync("auth/token", content, cancellationToken);
        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);

        IsConnected = await CheckHealthAsync(cancellationToken);
    }

    public async Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync("health", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            ServiceStatus = $"HTTP {(int)response.StatusCode}";
            return false;
        }

        var health = await response.Content.ReadFromJsonAsync<HealthResponse>(cancellationToken);
        ServiceStatus = health?.Status;
        return health?.Status == "Healthy";
    }

    private sealed record TokenResponse([property: JsonPropertyName("access_token")] string AccessToken);

    private sealed record HealthResponse(string Status);
}
