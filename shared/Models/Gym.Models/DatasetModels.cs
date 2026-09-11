using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gym.Models.Data;

// Shapes of the JSON files in shared/MockData. Nullable members are nullable because
// the malformed datasets really do omit them.

public sealed record CustomerRecord(string Id, string Name, string Region, string Tier);

public sealed record ProjectRecord(
    string Id,
    string CustomerId,
    string Name,
    string? ParentId,
    DateTimeOffset? CreatedAt);

public sealed record FileRecord(
    string Id,
    string ProjectId,
    string RelativePath,
    string Kind,
    long SizeBytes,
    string? Sha256,
    string? ContentType,
    Dictionary<string, JsonElement>? Metadata);

public sealed record MigrationRecordData(
    string Id,
    string CustomerId,
    string DestinationId,
    string Name,
    string State,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt);

public sealed record MigrationCheckpoint(
    string MigrationId,
    string CustomerId,
    string Stage,
    DateTimeOffset LastUpdated,
    int TotalFiles,
    List<FileCheckpoint> Files);

public sealed record FileCheckpoint(
    string FileId,
    string RelativePath,
    string Status,
    long BytesSent,
    string? RemoteFileId,
    string? UploadId);

public static class MockDataJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
