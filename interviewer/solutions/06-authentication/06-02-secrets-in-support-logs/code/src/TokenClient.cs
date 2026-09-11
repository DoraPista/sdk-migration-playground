using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace MigrationKit.Diagnostics;

public sealed class TokenClient
{
    private readonly HttpClient _http;
    private readonly SdkCredentials _credentials;
    private readonly ILogger<TokenClient> _logger;

    public TokenClient(HttpClient http, SdkCredentials credentials, ILogger<TokenClient> logger)
    {
        _http = http;
        _credentials = credentials;
        _logger = logger;
    }

    public async Task<string> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        // Log identifiers, never the credential object itself.
        _logger.LogInformation("Requesting an access token for client {ClientId}", _credentials.ClientId);

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = _credentials.ClientId,
            ["client_secret"] = _credentials.ClientSecret,
        });

        using var response = await _http.PostAsync("auth/token", content, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        // The token endpoint's body IS the secret on success, and may echo sensitive data on failure:
        // never log it. Log the status and the (non-secret) OAuth error code instead.
        _logger.LogDebug("Token endpoint answered {Status} for client {ClientId}", (int)response.StatusCode, _credentials.ClientId);

        if (!response.IsSuccessStatusCode)
        {
            var errorCode = TryGetOAuthError(body) ?? "unknown_error";
            _logger.LogWarning("Token request for client {ClientId} failed: {Status} {Error}", _credentials.ClientId, (int)response.StatusCode, errorCode);
            throw new InvalidOperationException($"Token request failed for client '{_credentials.ClientId}': {(int)response.StatusCode} {errorCode}.");
        }

        using var json = JsonDocument.Parse(body);
        return json.RootElement.GetProperty("access_token").GetString()!;
    }

    private static string? TryGetOAuthError(string body)
    {
        try
        {
            using var json = JsonDocument.Parse(body);
            return json.RootElement.TryGetProperty("error", out var error) ? error.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
