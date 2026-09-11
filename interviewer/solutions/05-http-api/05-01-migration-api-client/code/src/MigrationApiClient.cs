using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MigrationKit.Api;

public sealed class MigrationApiClient
{
    private const string CorrelationHeader = "X-Correlation-ID";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly HttpClient _http;
    private readonly MigrationApiOptions _options;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private AccessToken? _token;

    /// <param name="httpClient">Supplied by the host, with <see cref="HttpClient.BaseAddress"/> set to the platform URL.</param>
    public MigrationApiClient(HttpClient httpClient, MigrationApiOptions options)
        : this(httpClient, options, TimeProvider.System)
    {
    }

    internal MigrationApiClient(HttpClient httpClient, MigrationApiOptions options, TimeProvider time)
    {
        _http = httpClient;
        _options = options;
        _time = time;
    }

    public Task<Migration> CreateMigrationAsync(CreateMigrationRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<Migration>(HttpMethod.Post, "migrations", JsonContent.Create(request, options: Json), cancellationToken);

    public Task<Migration> GetMigrationAsync(string migrationId, CancellationToken cancellationToken = default) =>
        SendAsync<Migration>(HttpMethod.Get, $"migrations/{Uri.EscapeDataString(migrationId)}", null, cancellationToken);

    public Task<MigrationStatus> GetStatusAsync(string migrationId, CancellationToken cancellationToken = default) =>
        SendAsync<MigrationStatus>(HttpMethod.Get, $"migrations/{Uri.EscapeDataString(migrationId)}/status", null, cancellationToken);

    // ------------------------------------------------------------------ plumbing

    private async Task<T> SendAsync<T>(HttpMethod method, string path, HttpContent? content, CancellationToken cancellationToken)
    {
        var token = await GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        var correlationId = Guid.NewGuid().ToString("N");

        using var request = new HttpRequestMessage(method, path) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add(CorrelationHeader, correlationId);

        using var response = await SendCoreAsync(request, correlationId, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw await CreateErrorAsync(response, correlationId, cancellationToken).ConfigureAwait(false);
        }

        return await ReadJsonAsync<T>(response, correlationId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> SendCoreAsync(HttpRequestMessage request, string correlationId, CancellationToken cancellationToken)
    {
        try
        {
            return await _http.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new MigrationApiException($"The migration platform could not be reached ({ex.Message}).", null, correlationId, ex);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient.Timeout, not the caller: a failure, not a cancellation.
            throw new MigrationApiException("The migration platform did not respond in time.", null, correlationId, ex);
        }
    }

    private static async Task<T> ReadJsonAsync<T>(HttpResponseMessage response, string correlationId, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken).ConfigureAwait(false)
                   ?? throw new JsonException("The response body was empty.");
        }
        catch (JsonException ex)
        {
            throw new MigrationApiException($"The migration platform returned an unreadable response: {ex.Message}", response.StatusCode, correlationId, ex);
        }
    }

    private static async Task<MigrationApiException> CreateErrorAsync(HttpResponseMessage response, string correlationId, CancellationToken cancellationToken)
    {
        var problem = await TryReadProblemAsync(response, cancellationToken).ConfigureAwait(false);
        var message = problem?.Detail ?? problem?.Title ?? $"The migration platform returned {(int)response.StatusCode} {response.ReasonPhrase}.";

        return response.StatusCode switch
        {
            HttpStatusCode.NotFound => new MigrationNotFoundException(message, correlationId),
            HttpStatusCode.BadRequest => new MigrationValidationException(message, problem?.Errors ?? new Dictionary<string, string[]>(), correlationId),
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => new MigrationAuthenticationException(message, response.StatusCode, correlationId),
            _ => new MigrationApiException(message, response.StatusCode, correlationId),
        };
    }

    private static async Task<Problem?> TryReadProblemAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(text) ? null : JsonSerializer.Deserialize<Problem>(text, Json);
        }
        catch (JsonException)
        {
            return null; // an error page from a proxy, for example
        }
    }

    // ------------------------------------------------------------------ authentication

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (_token is { } cached && cached.IsValidAt(_time.GetUtcNow()))
        {
            return cached.Value;
        }

        // One token request at a time: concurrent callers wait for the same refresh (no stampede).
        await _tokenLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_token is { } refreshed && refreshed.IsValidAt(_time.GetUtcNow()))
            {
                return refreshed.Value;
            }

            _token = await RequestTokenAsync(cancellationToken).ConfigureAwait(false);
            return _token.Value;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private async Task<AccessToken> RequestTokenAsync(CancellationToken cancellationToken)
    {
        var correlationId = Guid.NewGuid().ToString("N");
        using var request = new HttpRequestMessage(HttpMethod.Post, "auth/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret,
            }),
        };
        request.Headers.Add(CorrelationHeader, correlationId);

        using var response = await SendCoreAsync(request, correlationId, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
        {
            throw new MigrationAuthenticationException("The migration platform rejected the client credentials.", response.StatusCode, correlationId);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw await CreateErrorAsync(response, correlationId, cancellationToken).ConfigureAwait(false);
        }

        var body = await ReadJsonAsync<TokenResponse>(response, correlationId, cancellationToken).ConfigureAwait(false);

        // Refresh a little early so a token never expires between "valid" and "used".
        var lifetime = TimeSpan.FromSeconds(Math.Max(0, body.ExpiresIn - 60));
        return new AccessToken(body.AccessToken, _time.GetUtcNow() + lifetime);
    }

    private sealed record AccessToken(string Value, DateTimeOffset RefreshAfter)
    {
        public bool IsValidAt(DateTimeOffset now) => now < RefreshAfter;
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);

    private sealed record Problem(string? Title, string? Detail, Dictionary<string, string[]>? Errors);
}
