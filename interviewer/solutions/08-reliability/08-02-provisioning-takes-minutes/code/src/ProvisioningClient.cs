using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace MigrationKit.Provisioning;

public sealed record Destination(string DestinationId, string Region);

public sealed class ProvisioningException(string message, string? operationId = null, Exception? innerException = null)
    : Exception(message, innerException)
{
    /// <summary>The platform's operation id, if provisioning got that far (support can look it up).</summary>
    public string? OperationId { get; } = operationId;
}

public sealed class ProvisioningClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan Deadline = TimeSpan.FromMinutes(10);            // the platform's own limit
    private static readonly TimeSpan DefaultPollInterval = TimeSpan.FromSeconds(5);  // when the server gives no hint
    private static readonly TimeSpan MinPollInterval = TimeSpan.FromSeconds(1);      // never spin
    private static readonly TimeSpan MaxPollInterval = TimeSpan.FromSeconds(60);     // never sleep past most of the deadline

    private readonly HttpClient _http;
    private readonly TimeProvider _time;

    public ProvisioningClient(HttpClient http, TimeProvider? time = null)
    {
        _http = http;
        _time = time ?? TimeProvider.System;
    }

    /// <summary>Provisions (or finds) the customer's destination and returns it once it is ready to use.</summary>
    public async Task<Destination> ProvisionAsync(string customerId, string region, CancellationToken cancellationToken = default)
    {
        var started = _time.GetUtcNow();
        using var response = await _http.PostAsJsonAsync("provision", new { customerId, region }, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK)
        {
            return await ReadDestinationAsync(response, null, cancellationToken).ConfigureAwait(false);
        }

        if (response.StatusCode != HttpStatusCode.Accepted)
        {
            throw new ProvisioningException($"The platform refused to provision a destination ({(int)response.StatusCode}).");
        }

        var accepted = await response.Content.ReadFromJsonAsync<OperationStatus>(Json, cancellationToken).ConfigureAwait(false);
        var operationId = accepted?.OperationId;
        var location = response.Headers.TryGetValues("Operation-Location", out var values) ? values.First() : $"operations/{operationId}";
        var wait = PollDelay(response);

        while (true)
        {
            // Deadline check BEFORE sleeping, so we never oversleep it by a whole interval.
            var remaining = Deadline - (_time.GetUtcNow() - started);
            if (remaining <= TimeSpan.Zero)
            {
                throw new ProvisioningException($"Provisioning did not finish within {Deadline.TotalMinutes:0} minutes.", operationId);
            }

            await Task.Delay(wait < remaining ? wait : remaining, _time, cancellationToken).ConfigureAwait(false);

            HttpResponseMessage poll;
            try
            {
                poll = await _http.GetAsync(location.TrimStart('/'), cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException)
            {
                // The OPERATION is still running on the server. Losing track of it would orphan the destination.
                wait = DefaultPollInterval;
                continue;
            }

            using (poll)
            {
                if ((int)poll.StatusCode >= 500 || poll.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    wait = PollDelay(poll);
                    continue;
                }

                if (!poll.IsSuccessStatusCode)
                {
                    throw new ProvisioningException($"Checking the provisioning operation failed ({(int)poll.StatusCode}).", operationId);
                }

                var status = await poll.Content.ReadFromJsonAsync<OperationStatus>(Json, cancellationToken).ConfigureAwait(false);
                switch (status?.Status)
                {
                    case "Succeeded" when status.Destination is { } destination:
                        return destination;
                    case "Failed" or "Canceled" or "Cancelled":
                        throw new ProvisioningException(
                            $"Provisioning failed: {status.Error?.Message ?? status.Status} ({status.Error?.Code ?? "no code"}).", operationId);
                    default:
                        wait = PollDelay(poll); // Running, NotStarted, or a status this SDK doesn't know yet: keep waiting
                        break;
                }
            }
        }
    }

    private static TimeSpan PollDelay(HttpResponseMessage response)
    {
        var hint = response.Headers.RetryAfter?.Delta ?? DefaultPollInterval;
        return hint < MinPollInterval ? MinPollInterval : hint > MaxPollInterval ? MaxPollInterval : hint;
    }

    private static async Task<Destination> ReadDestinationAsync(HttpResponseMessage response, string? operationId, CancellationToken cancellationToken)
    {
        var destination = await response.Content.ReadFromJsonAsync<Destination>(Json, cancellationToken).ConfigureAwait(false);
        return destination is { DestinationId.Length: > 0 }
            ? destination
            : throw new ProvisioningException("The platform returned a destination without an id.", operationId);
    }

    private sealed record OperationStatus(string? OperationId, string? Status, Destination? Destination, OperationError? Error);

    private sealed record OperationError(string? Code, string? Message);
}
