using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace MigrationKit.Client;

public sealed class MigrationClient
{
    private readonly HttpClient _http;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private AuthenticationHeaderValue? _authorization;

    public MigrationClient(HttpClient http, string clientId, string clientSecret)
    {
        _http = http;
        _clientId = clientId;
        _clientSecret = clientSecret;
    }

    public bool IsConnected { get; private set; }

    public string? ServiceStatus { get; private set; }

    // ------------------------------------------------------------------
    // Synchronous API: kept for existing WPF callers.
    // Still blocks the calling thread for the duration of the call (the UI freezes briefly), but no
    // longer deadlocks: the async implementation never needs the caller's thread to make progress.
    // GetAwaiter().GetResult() rethrows the original exception instead of an AggregateException.
    // ------------------------------------------------------------------

    [Obsolete("Blocks the calling thread. Use ConnectAsync from UI code.")]
    public void Connect() => ConnectAsync().GetAwaiter().GetResult();

    [Obsolete("Blocks the calling thread. Use CheckHealthAsync from UI code.")]
    public bool CheckHealth() => CheckHealthAsync().GetAwaiter().GetResult();

    // ------------------------------------------------------------------
    // Asynchronous API. Library code: ConfigureAwait(false) on every await, because the SDK never
    // needs to come back to the caller's UI thread. The CALLER still resumes on its own context,
    // since that's decided by the caller's await, not ours.
    // ------------------------------------------------------------------

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = _clientId,
            ["client_secret"] = _clientSecret,
        });

        using var response = await _http.PostAsync("auth/token", content, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken).ConfigureAwait(false);

        // Per-request header instead of mutating HttpClient.DefaultRequestHeaders, which isn't
        // thread-safe and would leak this token to anyone else sharing the HttpClient.
        _authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);

        IsConnected = await CheckHealthAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "health");
        request.Headers.Authorization = _authorization;

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            ServiceStatus = $"HTTP {(int)response.StatusCode}";
            return false;
        }

        var health = await response.Content.ReadFromJsonAsync<HealthResponse>(cancellationToken).ConfigureAwait(false);
        ServiceStatus = health?.Status;
        return health?.Status == "Healthy";
    }

    private sealed record TokenResponse([property: JsonPropertyName("access_token")] string AccessToken);

    private sealed record HealthResponse(string Status);
}
