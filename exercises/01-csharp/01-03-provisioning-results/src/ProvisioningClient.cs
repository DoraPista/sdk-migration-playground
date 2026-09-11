using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace MigrationKit.Provisioning;

internal sealed class ProvisioningClient
{
    private readonly HttpClient _http;

    public ProvisioningClient(HttpClient http)
    {
        _http = http;
    }

    /// <summary>
    /// Provisions a destination for the customer.
    /// Returns the destination id, or null if provisioning was not performed.
    /// Returns "-1" when the service could not be reached and "RETRY" when the service is throttling.
    /// </summary>
    public async Task<string?> ProvisionAsync(string customerId, string region, CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsJsonAsync("provision", new { customerId, region }, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return "-1";
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return null;
        }

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            return "RETRY";
        }

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            throw new ArgumentException(await response.Content.ReadAsStringAsync(cancellationToken));
        }

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return body.GetProperty("destinationId").GetString();
    }
}
