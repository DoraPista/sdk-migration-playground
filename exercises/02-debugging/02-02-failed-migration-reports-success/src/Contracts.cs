namespace MigrationKit.Migration;

public enum MigrationStatus
{
    Succeeded,
    PartiallySucceeded,
    Failed,
}

public sealed record FileFailure(string RelativePath, string Reason);

public sealed record MigrationResult(
    MigrationStatus Status,
    int FilesUploaded,
    IReadOnlyList<FileFailure> FailedFiles,
    string? Error);

/// <summary>The platform operations used by a migration (implemented over HTTP in production).</summary>
public interface IMigrationApi
{
    Task UploadManifestAsync(IReadOnlyList<string> relativePaths, CancellationToken cancellationToken);

    Task UploadFileAsync(string relativePath, Stream content, CancellationToken cancellationToken);

    Task CompleteAsync(CancellationToken cancellationToken);
}
