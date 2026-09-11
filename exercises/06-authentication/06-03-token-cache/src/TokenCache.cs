namespace MigrationKit.Tokens;

public sealed record TokenRequestContext(string TenantId, string ClientId, string Scope);

public sealed record IssuedToken(string AccessToken, int ExpiresInSeconds);

public interface IAuthorizationServer
{
    Task<IssuedToken> RequestTokenAsync(TokenRequestContext context, CancellationToken cancellationToken);
}

public sealed class TokenCache
{
    private static readonly Dictionary<string, CachedToken> Cache = new();

    private readonly IAuthorizationServer _server;
    private readonly TimeProvider _time;

    public TokenCache(IAuthorizationServer server, TimeProvider? time = null)
    {
        _server = server;
        _time = time ?? TimeProvider.System;
    }

    public async Task<string> GetAccessTokenAsync(TokenRequestContext context, CancellationToken cancellationToken = default)
    {
        if (Cache.TryGetValue(context.ClientId, out var cached) && cached.ExpiresAt > DateTime.Now)
        {
            return cached.AccessToken;
        }

        var issued = await _server.RequestTokenAsync(context, cancellationToken);
        Cache[context.ClientId] = new CachedToken(issued.AccessToken, DateTime.Now.AddSeconds(issued.ExpiresInSeconds));
        return issued.AccessToken;
    }

    private sealed record CachedToken(string AccessToken, DateTime ExpiresAt);
}
