using System.Net.Http.Json;
using System.Text.Json;

namespace MigrationKit.Api;

public sealed class MigrationApiClient
{
    private readonly HttpClient _http;
    private readonly MigrationApiOptions _options;

    /// <param name="httpClient">Supplied by the host, with <see cref="HttpClient.BaseAddress"/> set to the platform URL.</param>
    public MigrationApiClient(HttpClient httpClient, MigrationApiOptions options)
    {
        _http = httpClient;
        _options = options;
    }

    public async Task<Migration> CreateMigrationAsync(CreateMigrationRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _http.PostAsJsonAsync("migrations", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Migration>(cancellationToken))!;
    }

    public async Task<Migration> GetMigrationAsync(string migrationId, CancellationToken cancellationToken = default)
    {
        var json = await _http.GetStringAsync($"migrations/{migrationId}", cancellationToken);
        return JsonSerializer.Deserialize<Migration>(json)!;
    }

    public Task<MigrationStatus> GetStatusAsync(string migrationId, CancellationToken cancellationToken = default)
    {
        // TODO
        throw new NotImplementedException();
    }
}
