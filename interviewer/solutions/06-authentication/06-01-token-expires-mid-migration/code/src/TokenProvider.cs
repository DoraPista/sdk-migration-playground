using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace MigrationKit.Auth;

public sealed class AuthenticationFailedException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public interface ITokenProvider
{
    Task<string> GetTokenAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Replaces <paramref name="rejectedToken"/> with a new token. If another caller has already replaced it,
    /// returns the current token without contacting the identity service (single-flight refresh).
    /// </summary>
    Task<string> RefreshTokenAsync(string rejectedToken, CancellationToken cancellationToken);
}

/// <summary>Obtains access tokens from the platform's token endpoint (OAuth2 client credentials).</summary>
public sealed class TokenProvider : ITokenProvider
{
    private readonly HttpClient _tokenEndpoint;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private CachedToken? _token;

    /// <param name="tokenEndpoint">An HttpClient for the identity service (BaseAddress set, no authentication handler).</param>
    public TokenProvider(HttpClient tokenEndpoint, string clientId, string clientSecret)
        : this(tokenEndpoint, clientId, clientSecret, TimeProvider.System)
    {
    }

    internal TokenProvider(HttpClient tokenEndpoint, string clientId, string clientSecret, TimeProvider time)
    {
        _tokenEndpoint = tokenEndpoint;
        _clientId = clientId;
        _clientSecret = clientSecret;
        _time = time;
    }

    public async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        // Fast path without locking: a current, not-about-to-expire token.
        if (Volatile.Read(ref _token) is { } cached && cached.IsFreshAt(_time.GetUtcNow()))
        {
            return cached.Value;
        }

        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_token is { } current && current.IsFreshAt(_time.GetUtcNow()))
            {
                return current.Value;
            }

            // Proactive renewal: avoids a guaranteed 401 round-trip every hour.
            _token = await RequestTokenAsync(cancellationToken).ConfigureAwait(false);
            return _token.Value;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public async Task<string> RefreshTokenAsync(string rejectedToken, CancellationToken cancellationToken)
    {
        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_token is { } current && current.Value != rejectedToken)
            {
                return current.Value; // someone else already refreshed while we waited for the lock
            }

            _token = await RequestTokenAsync(cancellationToken).ConfigureAwait(false);
            return _token.Value;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task<CachedToken> RequestTokenAsync(CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = _clientId,
            ["client_secret"] = _clientSecret,
        });

        using var response = await _tokenEndpoint.PostAsync("auth/token", content, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new AuthenticationFailedException($"The identity service rejected the client credentials ({(int)response.StatusCode}).");
        }

        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken).ConfigureAwait(false)
                    ?? throw new AuthenticationFailedException("The identity service returned an empty token response.");

        // Renew a minute early (or at half-life for very short tokens) so a token never expires mid-request.
        var lifetime = TimeSpan.FromSeconds(token.ExpiresIn);
        var margin = TimeSpan.FromSeconds(Math.Min(60, token.ExpiresIn / 2.0));
        return new CachedToken(token.AccessToken, _time.GetUtcNow() + lifetime - margin);
    }

    private sealed record CachedToken(string Value, DateTimeOffset RenewAfter)
    {
        public bool IsFreshAt(DateTimeOffset now) => now < RenewAfter;
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
