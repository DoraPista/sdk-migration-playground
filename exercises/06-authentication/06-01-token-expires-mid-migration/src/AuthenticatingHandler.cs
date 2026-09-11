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
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await _tokens.GetTokenAsync(cancellationToken));
        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            // The token has expired: get a new one and try again.
            response.Dispose();
            await _tokens.RefreshTokenAsync(cancellationToken);
            return await SendAsync(request, cancellationToken);
        }

        return response;
    }
}
