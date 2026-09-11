using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Gym.TestUtilities;

public delegate Task<HttpResponseMessage> Responder(HttpRequestMessage request, CancellationToken cancellationToken);

/// <summary>What a <see cref="ScriptedHttpHandler"/> saw. Bodies are hashed while streaming, not buffered.</summary>
public sealed record RecordedRequest(
    HttpMethod Method,
    Uri Uri,
    IReadOnlyDictionary<string, string> Headers,
    long BodyLength,
    string? BodySha256,
    string? BodyText)
{
    public string Path => Uri.AbsolutePath;

    public string? Header(string name) => Headers.TryGetValue(name, out var value) ? value : null;
}

/// <summary>
/// A deterministic fake HTTP backend. Script responses in the order they should be returned;
/// optionally restrict a script step to matching requests.
/// <code>
/// var handler = new ScriptedHttpHandler()
///     .RespondWith(HttpStatusCode.ServiceUnavailable)
///     .RespondWith(HttpStatusCode.ServiceUnavailable)
///     .RespondJson(HttpStatusCode.OK, new { id = "mig-1" });
/// var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.test/") };
/// </code>
/// </summary>
public sealed class ScriptedHttpHandler : HttpMessageHandler
{
    private readonly object _gate = new();
    private readonly List<Step> _steps = new();
    private readonly ConcurrentQueue<RecordedRequest> _requests = new();
    private Responder _fallback = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    private int _inFlight;
    private int _peakInFlight;

    public IReadOnlyList<RecordedRequest> Requests => _requests.ToArray();

    public int CallCount => _requests.Count;

    public int PeakConcurrentRequests => Volatile.Read(ref _peakInFlight);

    /// <summary>Largest request body seen (bytes).</summary>
    public long LargestBody => _requests.IsEmpty ? 0 : _requests.Max(r => r.BodyLength);

    public ScriptedHttpHandler Enqueue(Responder responder, Func<HttpRequestMessage, bool>? when = null, int times = 1)
    {
        lock (_gate)
        {
            _steps.Add(new Step(responder, when, times));
        }

        return this;
    }

    /// <summary>Used once all scripted steps are consumed (default: 200 with no body).</summary>
    public ScriptedHttpHandler Otherwise(Responder responder)
    {
        _fallback = responder;
        return this;
    }

    public ScriptedHttpHandler OtherwiseRespond(HttpStatusCode status, object? json = null) =>
        Otherwise((_, _) => Task.FromResult(Responses.Json(status, json)));

    public ScriptedHttpHandler RespondWith(HttpStatusCode status, Func<HttpRequestMessage, bool>? when = null, int times = 1) =>
        Enqueue((_, _) => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(string.Empty) }), when, times);

    public ScriptedHttpHandler RespondJson(HttpStatusCode status, object? body, Func<HttpRequestMessage, bool>? when = null, int times = 1) =>
        Enqueue((_, _) => Task.FromResult(Responses.Json(status, body)), when, times);

    public ScriptedHttpHandler RespondRaw(HttpStatusCode status, string body, string mediaType = "application/json", Func<HttpRequestMessage, bool>? when = null) =>
        Enqueue((_, _) => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, mediaType) }), when);

    public ScriptedHttpHandler RespondSequence(params HttpStatusCode[] statuses)
    {
        foreach (var status in statuses)
        {
            RespondWith(status);
        }

        return this;
    }

    public ScriptedHttpHandler RespondTooManyRequests(TimeSpan retryAfter, Func<HttpRequestMessage, bool>? when = null) =>
        Enqueue((_, _) => Task.FromResult(Responses.TooManyRequests(retryAfter)), when);

    /// <summary>Simulates "connection refused / reset" the way SocketsHttpHandler reports it.</summary>
    public ScriptedHttpHandler FailConnection(Func<HttpRequestMessage, bool>? when = null, int times = 1) =>
        Enqueue((_, _) => throw new HttpRequestException(
            "An error occurred while sending the request.",
            new IOException("Unable to read data from the transport connection.", new SocketException((int)SocketError.ConnectionReset)),
            statusCode: null), when, times);

    /// <summary>Never answers; completes only when the request is cancelled (e.g. by HttpClient.Timeout).</summary>
    public ScriptedHttpHandler Hang(Func<HttpRequestMessage, bool>? when = null, int times = 1) =>
        Enqueue(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            throw new InvalidOperationException("unreachable");
        }, when, times);

    // Real handlers never need the caller's synchronization context (SocketsHttpHandler uses ConfigureAwait(false)
    // throughout). Running the fake on the thread pool keeps it just as context-free, so a deadlock in the
    // code under test is always the code's fault, never the fake's.
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.Run(() => SendCoreAsync(request, cancellationToken), cancellationToken);

    private async Task<HttpResponseMessage> SendCoreAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var current = Interlocked.Increment(ref _inFlight);
        UpdatePeak(current);
        try
        {
            _requests.Enqueue(await RecordAsync(request, cancellationToken));
            var responder = Next(request);
            var response = await responder(request, cancellationToken);
            response.RequestMessage ??= request;
            return response;
        }
        finally
        {
            Interlocked.Decrement(ref _inFlight);
        }
    }

    private Responder Next(HttpRequestMessage request)
    {
        lock (_gate)
        {
            for (var i = 0; i < _steps.Count; i++)
            {
                var step = _steps[i];
                if (step.When is not null && !step.When(request))
                {
                    continue;
                }

                if (--step.Remaining <= 0)
                {
                    _steps.RemoveAt(i);
                }

                return step.Responder;
            }

            return _fallback;
        }
    }

    private void UpdatePeak(int current)
    {
        int peak;
        while (current > (peak = Volatile.Read(ref _peakInFlight)))
        {
            if (Interlocked.CompareExchange(ref _peakInFlight, current, peak) == peak)
            {
                return;
            }
        }
    }

    private static async Task<RecordedRequest> RecordAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in request.Headers)
        {
            headers[header.Key] = string.Join(",", header.Value);
        }

        long length = 0;
        string? sha = null;
        string? text = null;
        if (request.Content is not null)
        {
            // Reading ContentLength makes HttpContent compute it (as SocketsHttpHandler does), so it shows up below.
            _ = request.Content.Headers.ContentLength;
            foreach (var header in request.Content.Headers)
            {
                headers[header.Key] = string.Join(",", header.Value);
            }

            // Serialize the content the way a real handler does (CopyToAsync -> SerializeToStreamAsync), so custom
            // HttpContent types are streamed rather than buffered, and large uploads stay cheap to record.
            using var sink = new RecordingSink();
            await request.Content.CopyToAsync(sink, cancellationToken);
            length = sink.BytesWritten;
            sha = sink.Sha256;
            text = sink.Text;
        }

        return new RecordedRequest(request.Method, request.RequestUri!, headers, length, sha, text);
    }

    private sealed class Step(Responder responder, Func<HttpRequestMessage, bool>? when, int times)
    {
        public Responder Responder { get; } = responder;
        public Func<HttpRequestMessage, bool>? When { get; } = when;
        public int Remaining { get; set; } = times;
    }
}

public static class Responses
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public static HttpResponseMessage Json(HttpStatusCode status, object? body) => new(status)
    {
        Content = body is null
            ? new StringContent(string.Empty)
            : new StringContent(JsonSerializer.Serialize(body, Web), Encoding.UTF8, "application/json"),
    };

    public static HttpResponseMessage Problem(HttpStatusCode status, string title, string? detail = null)
    {
        var response = Json(status, new { type = $"https://gym.local/problems/{(int)status}", title, status = (int)status, detail });
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/problem+json");
        return response;
    }

    public static HttpResponseMessage TooManyRequests(TimeSpan retryAfter)
    {
        var response = Problem(HttpStatusCode.TooManyRequests, "Too many requests");
        response.Headers.RetryAfter = new RetryConditionHeaderValue(retryAfter);
        return response;
    }

    public static HttpResponseMessage Unauthorized()
    {
        var response = Problem(HttpStatusCode.Unauthorized, "Unauthorized");
        response.Headers.WwwAuthenticate.ParseAdd("Bearer error=\"invalid_token\"");
        return response;
    }

    public static bool IsPath(this HttpRequestMessage request, string pathFragment) =>
        request.RequestUri?.AbsolutePath.Contains(pathFragment, StringComparison.OrdinalIgnoreCase) == true;
}
