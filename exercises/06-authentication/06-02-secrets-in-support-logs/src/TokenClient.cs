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
        _logger.LogInformation("Requesting an access token with {Credentials}", _credentials);

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = _credentials.ClientId,
            ["client_secret"] = _credentials.ClientSecret,
        });

        using var response = await _http.PostAsync("auth/token", content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogDebug("Token endpoint answered {Status}: {Body}", (int)response.StatusCode, body);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Token request failed for client '{_credentials.ClientId}' (secret {_credentials.ClientSecret[..6]}…): {body}");
        }

        using var json = JsonDocument.Parse(body);
        return json.RootElement.GetProperty("access_token").GetString()!;
    }
}
