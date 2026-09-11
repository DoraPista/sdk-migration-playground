using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace MigrationKit.Auth;

public sealed class AuthenticationFailedException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public interface ITokenProvider
{
    Task<string> GetTokenAsync(CancellationToken cancellationToken);

    Task<string> RefreshTokenAsync(CancellationToken cancellationToken);
}

/// <summary>Obtains access tokens from the platform's token endpoint (OAuth2 client credentials).</summary>
public sealed class TokenProvider : ITokenProvider
{
    private readonly HttpClient _tokenEndpoint;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private string? _token;

    /// <param name="tokenEndpoint">An HttpClient for the identity service (BaseAddress set, no authentication handler).</param>
    public TokenProvider(HttpClient tokenEndpoint, string clientId, string clientSecret)
    {
        _tokenEndpoint = tokenEndpoint;
        _clientId = clientId;
        _clientSecret = clientSecret;
    }

    public async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (_token is null)
        {
            _token = await RequestTokenAsync(cancellationToken);
        }

        return _token;
    }

    public async Task<string> RefreshTokenAsync(CancellationToken cancellationToken)
    {
        _token = await RequestTokenAsync(cancellationToken);
        return _token;
    }

    private async Task<string> RequestTokenAsync(CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = _clientId,
            ["client_secret"] = _clientSecret,
        });

        using var response = await _tokenEndpoint.PostAsync("auth/token", content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new AuthenticationFailedException($"The identity service rejected the client credentials ({(int)response.StatusCode}).");
        }

        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);
        return token!.AccessToken;
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
