using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;

namespace MigrationKit.Resilience;

public sealed class PlatformRequestException(string message, HttpStatusCode? statusCode, Exception? innerException = null)
    : Exception(message, innerException)
{
    /// <summary>The HTTP status, or null if no response was received.</summary>
    public HttpStatusCode? StatusCode { get; } = statusCode;
}

public sealed class MetadataUploader
{
    // Every number has a reason:
    private const int MaxAttempts = 6;                                        // 1 try + 5 retries: rides out a node recycle (5–30 s)
    private static readonly TimeSpan BaseDelay = TimeSpan.FromSeconds(1);     // first pause
    private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(30);     // no single pause longer than this
    private static readonly TimeSpan MaxRetryAfter = TimeSpan.FromMinutes(1); // don't let a server hint park us for an hour
    private static readonly TimeSpan RetryBudget = TimeSpan.FromMinutes(2);   // "give up after roughly two minutes"

    private readonly HttpClient _http;
    private readonly TimeProvider _time;

    public MetadataUploader(HttpClient http, TimeProvider? time = null)
    {
        _http = http;
        _time = time ?? TimeProvider.System;
    }

    /// <summary>Uploads a project's metadata document and returns the platform's file id.</summary>
    public async Task<string> UploadAsync(string migrationId, string projectId, byte[] metadataJson, CancellationToken cancellationToken = default)
    {
        var sha256 = Convert.ToHexStringLower(SHA256.HashData(metadataJson));
        var started = _time.GetUtcNow();

        for (var attempt = 1; ; attempt++)
        {
            var outcome = await TryOnceAsync(migrationId, projectId, metadataJson, sha256, cancellationToken).ConfigureAwait(false);
            if (outcome.FileId is { } fileId)
            {
                return fileId;
            }

            var failure = outcome.Failure!;
            if (!outcome.Transient || attempt >= MaxAttempts)
            {
                throw failure;
            }

            var delay = outcome.RetryAfter is { } hint
                ? Min(hint, MaxRetryAfter)                     // the server knows best when it will be ready
                : FullJitter(attempt);                         // otherwise: exponential backoff, randomised so clients spread out

            if (_time.GetUtcNow() - started + delay > RetryBudget)
            {
                throw failure;                                 // waiting would exceed the budget: fail now, not later
            }

            await Task.Delay(delay, _time, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<Outcome> TryOnceAsync(string migrationId, string projectId, byte[] body, string sha256, CancellationToken cancellationToken)
    {
        // A NEW request per attempt: a request message can only be sent once.
        using var request = new HttpRequestMessage(HttpMethod.Post, $"migrations/{migrationId}/files?projectId={Uri.EscapeDataString(projectId)}")
        {
            Content = new ByteArrayContent(body),
        };
        request.Headers.Add("X-File-Name", Uri.EscapeDataString($"metadata/{projectId}.json"));
        request.Headers.Add("X-Content-SHA256", sha256);

        // Retrying a POST is only safe because the platform de-duplicates on this key (see 07-03).
        request.Headers.Add("Idempotency-Key", $"meta-{migrationId}-{projectId}-{sha256[..16]}");

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            return Outcome.Failed(new PlatformRequestException("The platform could not be reached.", null, ex), transient: true);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            return Outcome.Failed(new PlatformRequestException("The platform did not respond in time.", null, ex), transient: true);
        }

        using (response)
        {
            if (response.IsSuccessStatusCode)
            {
                var stored = await response.Content.ReadFromJsonAsync<StoredFile>(cancellationToken).ConfigureAwait(false);
                return Outcome.Succeeded(stored!.FileId);
            }

            var error = new PlatformRequestException($"Metadata upload failed with {(int)response.StatusCode}.", response.StatusCode);
            return Outcome.Failed(error, IsTransient(response.StatusCode), RetryAfter(response));
        }
    }

    /// <summary>Transient: may succeed if repeated. Everything else (400, 401, 403, 404, 409, 422 …) never will.</summary>
    private static bool IsTransient(HttpStatusCode status) => status switch
    {
        HttpStatusCode.RequestTimeout => true,       // 408
        HttpStatusCode.TooManyRequests => true,      // 429
        HttpStatusCode.InternalServerError => true,  // 500
        HttpStatusCode.BadGateway => true,           // 502
        HttpStatusCode.ServiceUnavailable => true,   // 503
        HttpStatusCode.GatewayTimeout => true,       // 504
        _ => false,
    };

    private TimeSpan? RetryAfter(HttpResponseMessage response)
    {
        var header = response.Headers.RetryAfter;
        if (header?.Delta is { } delta)
        {
            return delta;
        }

        if (header?.Date is { } date)
        {
            var wait = date - _time.GetUtcNow();
            return wait > TimeSpan.Zero ? wait : TimeSpan.Zero;
        }

        return null;
    }

    /// <summary>"Full jitter": a random delay between 0 and min(cap, base * 2^(attempt-1)), with a small floor.</summary>
    private static TimeSpan FullJitter(int attempt)
    {
        var ceiling = Math.Min(MaxDelay.TotalMilliseconds, BaseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1));
        return TimeSpan.FromMilliseconds(Math.Max(200, Random.Shared.NextDouble() * ceiling));
    }

    private static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;

    private sealed record StoredFile(string FileId);

    private sealed record Outcome(string? FileId, PlatformRequestException? Failure, bool Transient, TimeSpan? RetryAfter)
    {
        public static Outcome Succeeded(string fileId) => new(fileId, null, false, null);

        public static Outcome Failed(PlatformRequestException failure, bool transient, TimeSpan? retryAfter = null) =>
            new(null, failure, transient, retryAfter);
    }
}
