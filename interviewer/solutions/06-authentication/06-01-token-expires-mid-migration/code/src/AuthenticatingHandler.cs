using System.Net;
using System.Net.Http.Headers;

namespace MigrationKit.Auth;

/// <summary>Adds the access token to every platform request and renews it when it has expired.</summary>
public sealed class AuthenticatingHandler : DelegatingHandler
{
    private readonly ITokenProvider _tokens;

    public AuthenticatingHandler(ITokenProvider tokens)
    {
        _tokens = tokens;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await _tokens.GetTokenAsync(cancellationToken).ConfigureAwait(false);
        var response = await SendWithTokenAsync(request, token, cancellationToken).ConfigureAwait(false);

        // Only 401 means "your credentials (token) are not valid". 403 means "valid, but not allowed":
        // a new token will not change that, so it goes straight back to the caller.
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        response.Dispose();
        var renewed = await _tokens.RefreshTokenAsync(token, cancellationToken).ConfigureAwait(false);

        // Exactly one retry. The request content must be replayable: ByteArrayContent is, and StreamContent over a
        // seekable stream is (it rewinds), but a non-seekable stream is not. Callers that stream should rebuild the request.
        var retry = await SendWithTokenAsync(request, renewed, cancellationToken).ConfigureAwait(false);
        if (retry.StatusCode == HttpStatusCode.Unauthorized)
        {
            retry.Dispose();
            throw new AuthenticationFailedException(
                "The migration platform rejected a newly issued access token. Check the client's app registration and permissions.");
        }

        return retry;
    }

    private Task<HttpResponseMessage> SendWithTokenAsync(HttpRequestMessage request, string token, CancellationToken cancellationToken)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return base.SendAsync(request, cancellationToken);
    }
}
