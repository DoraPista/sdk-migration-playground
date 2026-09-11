using Gym.TestUtilities;
using Microsoft.Extensions.Time.Testing;
using MigrationKit.Tokens;

namespace Ex0603.Tokens.Tests;

public sealed class TokenCacheTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 3, 29, 0, 30, 0, TimeSpan.Zero));

    [Fact]
    public async Task Token_is_reused_while_it_is_valid()
    {
        var server = new FakeAuthorizationServer(lifetimeSeconds: 3600);
        var cache = new TokenCache(server, _time);
        var context = Context("tenant-northwind");

        var first = await cache.GetAccessTokenAsync(context);
        _time.Advance(TimeSpan.FromMinutes(30));
        var second = await cache.GetAccessTokenAsync(context);

        Assert.Equal(first, second);
        Assert.Equal(1, server.Requests);
    }

    [Fact]
    public async Task Token_is_renewed_when_less_than_a_minute_of_lifetime_remains()
    {
        var server = new FakeAuthorizationServer(lifetimeSeconds: 3600);
        var cache = new TokenCache(server, _time);
        var context = Context("tenant-northwind");

        var first = await cache.GetAccessTokenAsync(context);
        _time.Advance(TimeSpan.FromSeconds(3600 - 45));
        var second = await cache.GetAccessTokenAsync(context);

        Assert.NotEqual(first, second);
        Assert.Equal(2, server.Requests);
    }

    [Fact]
    public async Task Expired_token_is_never_handed_out()
    {
        var server = new FakeAuthorizationServer(lifetimeSeconds: 600);
        var cache = new TokenCache(server, _time);
        var context = Context("tenant-northwind");

        var first = await cache.GetAccessTokenAsync(context);
        _time.Advance(TimeSpan.FromHours(2));

        Assert.NotEqual(first, await cache.GetAccessTokenAsync(context));
    }

    [Fact]
    public async Task Tenants_and_scopes_do_not_share_tokens()
    {
        var server = new FakeAuthorizationServer(lifetimeSeconds: 3600);
        var cache = new TokenCache(server, _time);

        var northwind = await cache.GetAccessTokenAsync(Context("tenant-northwind"));
        var contoso = await cache.GetAccessTokenAsync(Context("tenant-contoso"));
        var contosoAdmin = await cache.GetAccessTokenAsync(Context("tenant-contoso") with { Scope = "migration.admin" });

        Assert.Equal(3, new[] { northwind, contoso, contosoAdmin }.Distinct().Count());
        Assert.Equal(3, server.Requests);
    }

    [Fact]
    public async Task Separate_caches_do_not_share_tokens()
    {
        var context = Context("tenant-fabrikam");
        var serverA = new FakeAuthorizationServer(lifetimeSeconds: 3600, prefix: "A");
        var serverB = new FakeAuthorizationServer(lifetimeSeconds: 3600, prefix: "B");

        await new TokenCache(serverA, _time).GetAccessTokenAsync(context);
        var fromB = await new TokenCache(serverB, _time).GetAccessTokenAsync(context);

        Assert.StartsWith("B", fromB);
        Assert.Equal(1, serverB.Requests);
    }

    [Fact]
    public async Task Concurrent_callers_share_one_token_request()
    {
        var server = new FakeAuthorizationServer(lifetimeSeconds: 3600);
        var cache = new TokenCache(server, _time);
        var context = Context("tenant-tailspin");

        var tokens = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => cache.GetAccessTokenAsync(context))).WithTimeout();

        Assert.Single(tokens.Distinct());
        Assert.Equal(1, server.Requests);
    }

    private static TokenRequestContext Context(string tenant) => new(tenant, "consultant-desktop", "migration.write");

    private sealed class FakeAuthorizationServer(int lifetimeSeconds, string prefix = "tok") : IAuthorizationServer
    {
        private int _requests;

        public int Requests => Volatile.Read(ref _requests);

        public async Task<IssuedToken> RequestTokenAsync(TokenRequestContext context, CancellationToken cancellationToken)
        {
            var number = Interlocked.Increment(ref _requests);
            await Task.Delay(5, cancellationToken);
            return new IssuedToken($"{prefix}-{context.TenantId}-{number}", lifetimeSeconds);
        }
    }
}
