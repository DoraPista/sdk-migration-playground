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
        using var response = await _http.PostAsJsonAsync("provision", new { customerId, region }, cancellationToken);
        response.EnsureSuccessStatusCode();

        var destination = await response.Content.ReadFromJsonAsync<Destination>(Json, cancellationToken);
        return destination!;
    }
}
