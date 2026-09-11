using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using Gym.TestUtilities;
using Microsoft.Extensions.Time.Testing;
using MigrationKit.Provisioning;

namespace Ex0802.Provisioning.Tests;

public sealed class ProvisioningClientTests
{
    private readonly FakeTimeProvider _time = new();
    private readonly ConcurrentQueue<(string Method, DateTimeOffset At)> _calls = new();

    [Fact]
    public async Task Existing_destination_is_returned_immediately()
    {
        var platform = new ScriptedHttpHandler().Enqueue(Record(_ => Responses.Json(HttpStatusCode.Created, new { destinationId = "dst-00007", region = "westeurope" })));

        var destination = await RunAsync(platform);

        Assert.Equal(new Destination("dst-00007", "westeurope"), destination);
    }

    [Fact]
    public async Task Asynchronous_provisioning_is_followed_until_the_destination_is_ready()
    {
        var platform = Accepted(retryAfterSeconds: 5)
            .Enqueue(Record(_ => Operation("Running", retryAfterSeconds: 10)))
            .Enqueue(Record(_ => Operation("Running", retryAfterSeconds: 10)))
            .Enqueue(Record(_ => Operation("Succeeded")));

        var destination = await RunAsync(platform);

        Assert.Equal(new Destination("dst-00042", "westeurope"), destination);
        Assert.Equal(["POST", "GET", "GET", "GET"], _calls.Select(c => c.Method));
    }

    [Fact]
    public async Task Polling_follows_the_retry_after_hints()
    {
        var platform = Accepted(retryAfterSeconds: 5)
            .Enqueue(Record(_ => Operation("Running", retryAfterSeconds: 20)))
            .Enqueue(Record(_ => Operation("Succeeded")));

        await RunAsync(platform);

        var at = _calls.Select(c => c.At).ToArray();
        Assert.True(at[1] - at[0] >= TimeSpan.FromSeconds(5), "First poll came before the POST's Retry-After.");
        Assert.True(at[2] - at[1] >= TimeSpan.FromSeconds(20), "Second poll ignored the operation's Retry-After.");
    }

    [Fact]
    public async Task Failed_operation_is_reported_with_the_platform_error()
    {
        var platform = Accepted(retryAfterSeconds: 5)
            .Enqueue(Record(_ => Responses.Json(HttpStatusCode.OK, new
            {
                status = "Failed",
                error = new { code = "QuotaExceeded", message = "The subscription has reached its storage account quota." },
            })));

        var error = await Assert.ThrowsAsync<ProvisioningException>(() => RunAsync(platform));

        Assert.Contains("storage account quota", error.Message);
        Assert.Equal("op-123", error.OperationId);
    }

    [Fact]
    public async Task Operation_still_running_after_ten_minutes_is_reported_as_failed()
    {
        var platform = Accepted(retryAfterSeconds: 5).Otherwise(Record(_ => Operation("Running", retryAfterSeconds: 15)));

        var error = await Assert.ThrowsAsync<ProvisioningException>(() => RunAsync(platform));

        var elapsed = _time.GetUtcNow() - _calls.First().At;
        Assert.InRange(elapsed, TimeSpan.FromMinutes(9.5), TimeSpan.FromMinutes(11));
        Assert.Equal("op-123", error.OperationId);
    }

    [Fact]
    public async Task Temporary_errors_while_polling_do_not_abandon_the_operation()
    {
        var platform = Accepted(retryAfterSeconds: 5)
            .Enqueue(Record(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)))
            .Enqueue(Record(_ => new HttpResponseMessage(HttpStatusCode.BadGateway)))
            .Enqueue(Record(_ => Operation("Succeeded")));

        var destination = await RunAsync(platform);

        Assert.Equal("dst-00042", destination.DestinationId);
    }

    [Fact]
    public async Task Cancellation_stops_the_waiting_immediately()
    {
        var platform = Accepted(retryAfterSeconds: 60).Otherwise(Record(_ => Operation("Running", retryAfterSeconds: 60)));
        using var cts = new CancellationTokenSource();

        var provisioning = Client(platform).ProvisionAsync("CUST-1001", "westeurope", cts.Token);
        await Eventually.TrueAsync(() => !_calls.IsEmpty, TimeSpan.FromSeconds(5), "provisioning started");
        await Task.Delay(50);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provisioning.WithTimeout(TimeSpan.FromSeconds(2)));
    }

    private async Task<Destination> RunAsync(ScriptedHttpHandler platform)
    {
        var provisioning = Client(platform).ProvisionAsync("CUST-1001", "westeurope");
        await _time.AdvanceUntilCompleteAsync(provisioning, step: TimeSpan.FromSeconds(1), maxAdvance: TimeSpan.FromMinutes(30)).ContinueWith(_ => { });
        return await provisioning;
    }

    private ProvisioningClient Client(ScriptedHttpHandler platform) =>
        new(new HttpClient(platform) { BaseAddress = new Uri("https://platform.test/") }, _time);

    private ScriptedHttpHandler Accepted(int retryAfterSeconds) => new ScriptedHttpHandler().Enqueue(Record(_ =>
    {
        var response = Responses.Json(HttpStatusCode.Accepted, new { operationId = "op-123", status = "Running" });
        response.Headers.Add("Operation-Location", "/operations/op-123");
        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(retryAfterSeconds));
        return response;
    }));

    private static HttpResponseMessage Operation(string status, int? retryAfterSeconds = null)
    {
        var response = status == "Succeeded"
            ? Responses.Json(HttpStatusCode.OK, new { status, destination = new { destinationId = "dst-00042", region = "westeurope" } })
            : Responses.Json(HttpStatusCode.OK, new { status });
        if (retryAfterSeconds is { } seconds)
        {
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(seconds));
        }

        return response;
    }

    private Responder Record(Func<HttpRequestMessage, HttpResponseMessage> respond) => (request, _) =>
    {
        _calls.Enqueue((request.Method.Method, _time.GetUtcNow()));
        return Task.FromResult(respond(request));
    };
}
