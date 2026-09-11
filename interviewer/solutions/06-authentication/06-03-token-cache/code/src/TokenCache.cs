using System.Collections.Concurrent;

namespace MigrationKit.Tokens;

public sealed record TokenRequestContext(string TenantId, string ClientId, string Scope);

public sealed record IssuedToken(string AccessToken, int ExpiresInSeconds);

public interface IAuthorizationServer
{
    Task<IssuedToken> RequestTokenAsync(TokenRequestContext context, CancellationToken cancellationToken);
}

public sealed class TokenCache
{
    /// <summary>Agreed with the platform: a handed-out token has at least this much lifetime left.</summary>
    private static readonly TimeSpan MinimumRemainingLifetime = TimeSpan.FromSeconds(60);

    private readonly IAuthorizationServer _server;
    private readonly TimeProvider _time;

    // Per instance (not static): two SDK clients must never see each other's tokens.
    // Keyed by the WHOLE context (tenant + client + scope), and the value is the in-flight request,
    // so concurrent callers share one call to the authorization server.
    private readonly ConcurrentDictionary<TokenRequestContext, Task<CachedToken>> _tokens = new();

    public TokenCache(IAuthorizationServer server, TimeProvider? time = null)
    {
        _server = server;
        _time = time ?? TimeProvider.System;
    }

    public async Task<string> GetAccessTokenAsync(TokenRequestContext context, CancellationToken cancellationToken = default)
    {
        while (true)
        {
            var pending = _tokens.GetOrAdd(context, RequestAsync);
            CachedToken token;
            try
            {
                token = await pending.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch when (!cancellationToken.IsCancellationRequested)
            {
                // Never cache a failure: remove it (only if it's still the same entry) so the next caller retries.
                _tokens.TryRemove(new KeyValuePair<TokenRequestContext, Task<CachedToken>>(context, pending));
                throw;
            }

            if (token.IsUsableAt(_time.GetUtcNow(), MinimumRemainingLifetime))
            {
                return token.AccessToken;
            }

            // Too close to expiry: drop it (if nobody replaced it yet) and loop to get or join a fresh request.
            _tokens.TryRemove(new KeyValuePair<TokenRequestContext, Task<CachedToken>>(context, pending));
        }
    }

    private async Task<CachedToken> RequestAsync(TokenRequestContext context)
    {
        // Not the caller's token: one caller cancelling must not fail the shared request for everyone else.
        var issued = await _server.RequestTokenAsync(context, CancellationToken.None).ConfigureAwait(false);

        // UTC from an injectable clock: DateTime.Now is local time, which jumps at DST changes (problem 3),
        // and can't be controlled in tests.
        return new CachedToken(issued.AccessToken, _time.GetUtcNow().AddSeconds(issued.ExpiresInSeconds));
    }

    private sealed record CachedToken(string AccessToken, DateTimeOffset ExpiresAt)
    {
        public bool IsUsableAt(DateTimeOffset now, TimeSpan minimumRemaining) => ExpiresAt - now >= minimumRemaining;
    }
}
