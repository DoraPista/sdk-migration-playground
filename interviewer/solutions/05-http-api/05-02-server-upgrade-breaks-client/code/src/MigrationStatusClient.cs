using System.Text.Json;
using System.Text.Json.Serialization;

namespace MigrationKit.Status;

/// <remarks>
/// Consumers compile these numeric values into their binaries: never renumber existing members.
/// <see cref="Unknown"/> is explicit (-1) so the existing values keep their meaning.
/// </remarks>
public enum MigrationState
{
    /// <summary>A state this version of the SDK does not recognise. Treat as "in progress".</summary>
    Unknown = -1,
    Created = 0,
    Uploading = 1,
    Completed = 2,
}

public sealed record MigrationStatus(
    string Id,
    [property: JsonConverter(typeof(TolerantMigrationStateConverter))] MigrationState State,
    int FilesReceived,
    long BytesReceived,
    DateTimeOffset UpdatedAt);

public sealed class MigrationStatusClient
{
    /// <summary>The platform API version this SDK was built and tested against.</summary>
    internal const string ApiVersion = "1";

    // Tolerant reader: unknown properties are ignored (the default). Additive changes are allowed by the
    // platform's contract, so treating them as errors turns every harmless server release into an outage.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly TimeProvider _time;

    public MigrationStatusClient(HttpClient http, TimeProvider? time = null)
    {
        _http = http;
        _time = time ?? TimeProvider.System;
    }

    public async Task<MigrationStatus> GetStatusAsync(string migrationId, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"migrations/{Uri.EscapeDataString(migrationId)}/status");
        request.Headers.Add("api-version", ApiVersion);

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return (await JsonSerializer.DeserializeAsync<MigrationStatus>(body, Json, cancellationToken).ConfigureAwait(false))!;
    }

    /// <summary>Polls the platform until the migration has completed.</summary>
    public async Task<MigrationStatus> WaitForCompletionAsync(string migrationId, TimeSpan pollInterval, CancellationToken cancellationToken = default)
    {
        while (true)
        {
            var status = await GetStatusAsync(migrationId, cancellationToken).ConfigureAwait(false);
            if (status.State == MigrationState.Completed)
            {
                return status;
            }

            // Unknown states are deliberately treated as "not finished yet".
            await Task.Delay(pollInterval, _time, cancellationToken).ConfigureAwait(false);
        }
    }
}

/// <summary>Maps any unrecognised state string to <see cref="MigrationState.Unknown"/> instead of throwing.</summary>
internal sealed class TolerantMigrationStateConverter : JsonConverter<MigrationState>
{
    public override MigrationState Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String
            && Enum.TryParse<MigrationState>(reader.GetString(), ignoreCase: true, out var state)
            && Enum.IsDefined(state)
            && state != MigrationState.Unknown
            && !char.IsDigit(reader.GetString()![0]))
        {
            return state;
        }

        return MigrationState.Unknown;
    }

    public override void Write(Utf8JsonWriter writer, MigrationState value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}
