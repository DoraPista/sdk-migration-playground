using System.Collections.Concurrent;
using System.Net;
using Gym.TestUtilities;

namespace Ex0601.Auth.Tests;

/// <summary>
/// A fake identity service plus a fake migration API that only accepts tokens the identity service issued
/// and hasn't expired. Deterministic: the test decides when tokens expire.
/// </summary>
internal sealed class FakeIdentityPlatform
{
    private const int SafetyNet = 20;
    private readonly ConcurrentDictionary<string, bool> _validTokens = new();
    private int _tokenRequests;
    private int _unauthorized;

    public FakeIdentityPlatform()
    {
        Identity = new ScriptedHttpHandler().Otherwise(IssueTokenAsync);
        Api = new ScriptedHttpHandler().Otherwise(ServeApiAsync);
    }

    public ScriptedHttpHandler Identity { get; }

    public ScriptedHttpHandler Api { get; }

    public int TokenRequests => Volatile.Read(ref _tokenRequests);

    public int UnauthorizedResponses => Volatile.Read(ref _unauthorized);

    /// <summary>Runs before a token is issued (e.g. to hold the identity service until N requests have failed).</summary>
    public Func<Task>? BeforeIssuingToken { get; set; }

    /// <summary>Tokens are issued, but the API rejects all of them (misconfigured app registration).</summary>
    public bool ApiRejectsEveryToken { get; set; }

    /// <summary>The caller is authenticated but lacks permission.</summary>
    public bool ApiForbidsEverything { get; set; }

    public void ExpireAllTokens() => _validTokens.Clear();

    private async Task<HttpResponseMessage> IssueTokenAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var number = Interlocked.Increment(ref _tokenRequests);
        if (number > SafetyNet)
        {
            // Stops a looping client from hammering the test process forever.
            throw new InvalidOperationException($"Test safety net: more than {SafetyNet} token requests. The client is looping.");
        }

        if (BeforeIssuingToken is not null)
        {
            await BeforeIssuingToken();
        }

        var token = $"tok-{number}";
        _validTokens[token] = true;
        return Responses.Json(HttpStatusCode.OK, new Dictionary<string, object>
        {
            ["access_token"] = token,
            ["token_type"] = "Bearer",
            ["expires_in"] = 3600,
        });
    }

    private Task<HttpResponseMessage> ServeApiAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = request.Headers.Authorization?.Parameter;
        if (ApiRejectsEveryToken || token is null || !_validTokens.ContainsKey(token))
        {
            Interlocked.Increment(ref _unauthorized);
            return Task.FromResult(Responses.Unauthorized());
        }

        return Task.FromResult(ApiForbidsEverything
            ? Responses.Problem(HttpStatusCode.Forbidden, "The client lacks the Migration.Write permission.")
            : Responses.Json(HttpStatusCode.Created, new { fileId = "file-1" }));
    }
}
