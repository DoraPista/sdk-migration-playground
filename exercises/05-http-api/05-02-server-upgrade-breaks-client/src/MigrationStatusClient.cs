using System.Text.Json;
using System.Text.Json.Serialization;

namespace MigrationKit.Status;

public enum MigrationState
{
    Created,
    Uploading,
    Completed,
}

public sealed record MigrationStatus(
    string Id,
    MigrationState State,
    int FilesReceived,
    long BytesReceived,
    DateTimeOffset UpdatedAt);

public sealed class MigrationStatusClient
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,

        // Fail fast if the platform sends something we don't expect: contract mistakes surface early.
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly HttpClient _http;
    private readonly TimeProvider _time;

    public MigrationStatusClient(HttpClient http, TimeProvider? time = null)
    {
        _http = http;
        _time = time ?? TimeProvider.System;
    }

    public async Task<MigrationStatus> GetStatusAsync(string migrationId, CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync($"migrations/{Uri.EscapeDataString(migrationId)}/status", cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
        return (await JsonSerializer.DeserializeAsync<MigrationStatus>(body, Json, cancellationToken))!;
    }

    /// <summary>Polls the platform until the migration has completed.</summary>
    public async Task<MigrationStatus> WaitForCompletionAsync(string migrationId, TimeSpan pollInterval, CancellationToken cancellationToken = default)
    {
        while (true)
        {
            var status = await GetStatusAsync(migrationId, cancellationToken);
            if (status.State == MigrationState.Completed)
            {
                return status;
            }

            await Task.Delay(pollInterval, _time, cancellationToken);
        }
    }
}
