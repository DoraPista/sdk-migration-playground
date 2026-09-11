namespace MigrationKit.Api;

// ------------------------------------------------------------------
// Public contract (agreed in API review; do not change).
// ------------------------------------------------------------------

public sealed record MigrationApiOptions
{
    public required string ClientId { get; init; }

    public required string ClientSecret { get; init; }
}

public enum MigrationState
{
    Created,
    Uploading,
    Completed,
}

public sealed record CreateMigrationRequest(string CustomerId, string DestinationId, string Name);

public sealed record Migration(
    string Id,
    string CustomerId,
    string DestinationId,
    string Name,
    MigrationState State,
    int FileCount,
    long BytesReceived,
    DateTimeOffset CreatedAt);

public sealed record MigrationStatus(
    string Id,
    MigrationState State,
    int FilesReceived,
    long BytesReceived,
    DateTimeOffset UpdatedAt);
