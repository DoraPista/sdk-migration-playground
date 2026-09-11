using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace MigrationKit.Provisioning;

/// <summary>Every way provisioning can end. Callers must handle each case explicitly.</summary>
internal abstract record ProvisioningOutcome
{
    private ProvisioningOutcome()
    {
    }

    /// <summary>A destination exists (either just created, or it already existed).</summary>
    public sealed record Provisioned(string DestinationId, bool AlreadyExisted) : ProvisioningOutcome;

    /// <summary>The request was understood and rejected. Retrying the same request will not help.</summary>
    public sealed record Rejected(string Reason, IReadOnlyDictionary<string, string[]> Errors) : ProvisioningOutcome;

    /// <summary>Something that may go away on its own: network failure, throttling, server error.</summary>
    public sealed record TransientFailure(string Reason, TimeSpan? RetryAfter, Exception? Exception = null) : ProvisioningOutcome;
}

internal sealed class ProvisioningClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;

    public ProvisioningClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<ProvisioningOutcome> ProvisionAsync(string customerId, string region, CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsJsonAsync("provision", new { customerId, region }, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            return new ProvisioningOutcome.TransientFailure("The cloud platform could not be reached.", null, ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient.Timeout surfaces as TaskCanceledException, but it is not the user cancelling.
            return new ProvisioningOutcome.TransientFailure("The cloud platform did not respond in time.", null, ex);
        }

        using (response)
        {
            var problem = await ReadProblemAsync(response, cancellationToken);
            switch (response.StatusCode)
            {
                case HttpStatusCode.Created:
                case HttpStatusCode.OK:
                    return new ProvisioningOutcome.Provisioned(RequireDestinationId(problem), AlreadyExisted: false);

                case HttpStatusCode.Conflict when problem?.DestinationId is { Length: > 0 } existing:
                    return new ProvisioningOutcome.Provisioned(existing, AlreadyExisted: true);

                case HttpStatusCode.TooManyRequests:
                    return new ProvisioningOutcome.TransientFailure("The cloud platform is busy.", response.Headers.RetryAfter?.Delta);

                case >= HttpStatusCode.InternalServerError:
                    return new ProvisioningOutcome.TransientFailure($"The cloud platform returned {(int)response.StatusCode}.", response.Headers.RetryAfter?.Delta);

                default:
                    // 400, 403, 404, a 409 without a destination, ...: permanent from the client's point of view.
                    return new ProvisioningOutcome.Rejected(
                        problem?.Describe() ?? $"The cloud platform rejected the request ({(int)response.StatusCode}).",
                        problem?.Errors ?? new Dictionary<string, string[]>());
            }
        }
    }

    private static string RequireDestinationId(ProblemBody? body) =>
        body?.DestinationId is { Length: > 0 } id
            ? id
            : throw new InvalidDataException("The provisioning response did not contain a destinationId.");

    private static async Task<ProblemBody?> ReadProblemAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            return string.IsNullOrWhiteSpace(text) ? null : JsonSerializer.Deserialize<ProblemBody>(text, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record ProblemBody(string? Title, string? Detail, string? DestinationId, Dictionary<string, string[]>? Errors)
    {
        public string? Describe()
        {
            var details = Errors?.SelectMany(e => e.Value).ToArray() ?? [];
            if (details.Length > 0) return string.Join(" ", details);
            return Detail ?? Title;
        }
    }
}
