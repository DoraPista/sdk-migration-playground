namespace MigrationKit.Pipeline;

public sealed record SourceFile(string Id, string ProjectId, string RelativePath, long SizeBytes, string? Sha256, string Kind);

public sealed record MigrationJob(string CustomerId, IReadOnlyList<string> ProjectIds, IReadOnlyList<SourceFile> Files);

public sealed class MigrationOptions
{
    public bool UploadMetadata { get; set; } = true;

    public bool SkipVerification { get; set; }

    /// <summary>Runs everything except the actual uploads (used by support to reproduce problems).</summary>
    public bool DryRun { get; set; }

    public bool GenerateThumbnails { get; set; }
}

public sealed record ValidationIssue(string Code, string Subject, string Message);

public static class ValidationCodes
{
    public const string DuplicateFileId = "duplicate-file-id";
    public const string UnknownProject = "unknown-project";
    public const string NegativeSize = "negative-size";
    public const string InvalidHash = "invalid-hash";
    public const string UnsafePath = "unsafe-path";
    public const string EmptyPath = "empty-path";
}

public sealed record WorkflowReport(
    bool Succeeded,
    IReadOnlyList<string> StagesRun,
    IReadOnlyList<ValidationIssue> Issues,
    int FilesUploaded,
    int ThumbnailsGenerated,
    string? Error);

public interface IMigrationPlatform
{
    Task AuthenticateAsync(CancellationToken cancellationToken);

    Task<string> ProvisionAsync(string customerId, CancellationToken cancellationToken);

    Task<string> CreateMigrationAsync(string customerId, string destinationId, CancellationToken cancellationToken);

    Task UploadMetadataAsync(string migrationId, MigrationJob job, CancellationToken cancellationToken);

    Task UploadFileAsync(string migrationId, SourceFile file, CancellationToken cancellationToken);

    Task UploadThumbnailAsync(string migrationId, SourceFile file, byte[] thumbnail, CancellationToken cancellationToken);

    Task<bool> VerifyAsync(string migrationId, CancellationToken cancellationToken);

    Task CompleteAsync(string migrationId, CancellationToken cancellationToken);
}

/// <summary>Supplied by the host (WPF/MAUI render thumbnails with their own imaging stack).</summary>
public interface IThumbnailGenerator
{
    Task<byte[]> GenerateAsync(SourceFile file, CancellationToken cancellationToken);
}
