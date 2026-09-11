using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Gym.TestUtilities;
using Microsoft.Extensions.Time.Testing;
using MigrationKit.Resilience;

namespace Ex0801.Resilience.Tests;

public sealed class ResilienceTests
{
    private static readonly byte[] Metadata = Encoding.UTF8.GetBytes("""{"projectId":"PRJ-2002","name":"Harbour Bridge – Structural Survey"}""");
    private readonly FakeTimeProvider _time = new();
    private readonly ConcurrentQueue<DateTimeOffset> _attempts = new();
    private readonly ConcurrentQueue<string?> _bodies = new();

    [Fact]
    public async Task Healthy_platform_succeeds_at_the_first_attempt()
    {
        var platform = Script("201");

        var fileId = await RunAsync(platform);

        Assert.Equal("file-00001", fileId);
        Assert.Single(_attempts);
    }

    [Theory]
    [InlineData("503,503,201")]
    [InlineData("502,201")]
    [InlineData("500,504,201")]
    [InlineData("408,201")]
    [InlineData("connection-reset,201")]
    [InlineData("503,connection-reset,503,201")]
    public async Task Transient_failures_are_recovered_from(string sequence)
    {
        var platform = Script(sequence);

        var fileId = await RunAsync(platform);

        Assert.Equal("file-00001", fileId);
        Assert.Equal(sequence.Split(',').Length, _attempts.Count);
        Assert.Single(_bodies.Distinct()); // every attempt sent the complete, identical body
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Conflict)]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task Failures_that_cannot_succeed_are_not_retried(HttpStatusCode status)
    {
        var platform = Script(((int)status).ToString());

        var error = await Assert.ThrowsAsync<PlatformRequestException>(() => RunAsync(platform));

        Assert.Equal(status, error.StatusCode);
        Assert.Single(_attempts);
    }

    [Fact]
    public async Task Retry_after_from_the_platform_is_respected()
    {
        var platform = new ScriptedHttpHandler()
            .Enqueue(Record((_, _) => Task.FromResult(Responses.TooManyRequests(TimeSpan.FromSeconds(30)))))
            .Otherwise(Record((_, _) => Task.FromResult(Created())));

        await RunAsync(platform);

        var times = _attempts.ToArray();
        Assert.Equal(2, times.Length);
        Assert.True(times[1] - times[0] >= TimeSpan.FromSeconds(30), $"Retried after {(times[1] - times[0]).TotalSeconds}s despite Retry-After: 30.");
    }

    [Fact]
    public async Task There_is_a_pause_between_attempts()
    {
        var platform = Script("503,503,503,201");

        await RunAsync(platform);

        var times = _attempts.ToArray();
        var gaps = times.Zip(times.Skip(1), (a, b) => b - a).ToArray();
        Assert.All(gaps, gap => Assert.True(gap > TimeSpan.Zero, "Retried without waiting."));
        Assert.True(times[^1] - times[0] >= TimeSpan.FromSeconds(1), "The retries were squeezed into less than a second.");
    }

    [Fact]
    public async Task A_long_outage_ends_in_failure_within_bounds()
    {
        var platform = new ScriptedHttpHandler().Otherwise(Record((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))));

        var error = await Assert.ThrowsAsync<PlatformRequestException>(() => RunAsync(platform));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, error.StatusCode);
        Assert.InRange(_attempts.Count, 3, 10);
        Assert.True(_time.GetUtcNow() - _attempts.First() <= TimeSpan.FromMinutes(3), "Kept retrying for more than three minutes.");
    }

    [Fact]
    public async Task Cancelling_during_a_pause_stops_immediately()
    {
        var platform = new ScriptedHttpHandler().Otherwise(Record((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))));
        using var cts = new CancellationTokenSource();

        var upload = new MetadataUploader(Client(platform), _time).UploadAsync("mig-00102", "PRJ-2002", Metadata, cts.Token);
        await Eventually.TrueAsync(() => _attempts.Count == 1, TimeSpan.FromSeconds(5), "first attempt made");
        await Task.Delay(50);
        cts.Cancel(); // the fake clock is never advanced: only cancellation can end the pause

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => upload.WithTimeout(TimeSpan.FromSeconds(2)));
        Assert.Single(_attempts);
    }

    private async Task<string> RunAsync(ScriptedHttpHandler platform)
    {
        var upload = new MetadataUploader(Client(platform), _time).UploadAsync("mig-00102", "PRJ-2002", Metadata);
        await _time.AdvanceUntilCompleteAsync(upload, step: TimeSpan.FromSeconds(1), maxAdvance: TimeSpan.FromMinutes(30)).ContinueWith(_ => { });
        return await upload;
    }

    private ScriptedHttpHandler Script(string sequence)
    {
        var handler = new ScriptedHttpHandler();
        foreach (var step in sequence.Split(','))
        {
            handler.Enqueue(Record((_, _) => step switch
            {
                "connection-reset" => throw new HttpRequestException(
                    "An error occurred while sending the request.",
                    new IOException("Connection reset", new SocketException((int)SocketError.ConnectionReset))),
                "201" => Task.FromResult(Created()),
                _ => Task.FromResult(new HttpResponseMessage((HttpStatusCode)int.Parse(step)) { Content = new StringContent("{}") }),
            }));
        }

        return handler.Otherwise(Record((_, _) => Task.FromResult(Created())));
    }

    private Responder Record(Responder inner) => async (request, ct) =>
    {
        _attempts.Enqueue(_time.GetUtcNow());
        _bodies.Enqueue(Convert.ToBase64String(await request.Content!.ReadAsByteArrayAsync(ct)));
        return await inner(request, ct);
    };

    private static HttpResponseMessage Created() => Responses.Json(HttpStatusCode.Created, new { fileId = "file-00001" });

    private static HttpClient Client(HttpMessageHandler handler) => new(handler) { BaseAddress = new Uri("https://platform.test/") };
}
