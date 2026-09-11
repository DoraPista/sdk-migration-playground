using System.Text.Json.Serialization;

namespace Gym.Models.Api;

// Wire contract of the mock Migration Platform API (see shared/MockServer/API.md).
// Exercises deliberately do NOT reference these types: an SDK owns its own DTOs.

public sealed record TokenRequest(
    [property: JsonPropertyName("grant_type")] string? GrantType,
    [property: JsonPropertyName("client_id")] string? ClientId,
    [property: JsonPropertyName("client_secret")] string? ClientSecret);

public sealed record TokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("token_type")] string TokenType,
    [property: JsonPropertyName("expires_in")] int ExpiresIn);

public sealed record ProvisionRequest(string? CustomerId, string? Region);

public sealed record ProvisionResponse(string DestinationId, string CustomerId, string Region, DateTimeOffset CreatedAt);

public sealed record CreateMigrationRequest(string? CustomerId, string? DestinationId, string? Name);

public sealed record MigrationDto(
    string Id,
    string CustomerId,
    string DestinationId,
    string Name,
    string State,
    int FileCount,
    long BytesReceived,
    DateTimeOffset CreatedAt);

public sealed record MigrationStatusDto(
    string Id,
    string State,
    int FilesReceived,
    long BytesReceived,
    DateTimeOffset UpdatedAt);

public sealed record StoredFileDto(
    string FileId,
    string Name,
    string? ProjectId,
    long Size,
    string Sha256,
    DateTimeOffset StoredAt);

public sealed record StartUploadRequest(string? FileName, long Length, string? Sha256, string? ProjectId);

public sealed record UploadSessionDto(
    string UploadId,
    string MigrationId,
    string FileName,
    long Length,
    long Received,
    bool Completed);

public sealed record ProblemDto(
    string Type,
    string Title,
    int Status,
    string? Detail = null,
    IDictionary<string, string[]>? Errors = null);

public static class MigrationStates
{
    public const string Created = "Created";
    public const string Uploading = "Uploading";
    public const string Completed = "Completed";
}

public static class ApiHeaders
{
    public const string CorrelationId = "X-Correlation-ID";
    public const string IdempotencyKey = "Idempotency-Key";
    public const string FileName = "X-File-Name";
    public const string ContentSha256 = "X-Content-SHA256";
    public const string ApiVersion = "api-version";
}
