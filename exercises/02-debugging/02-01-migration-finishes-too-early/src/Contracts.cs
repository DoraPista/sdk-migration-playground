namespace MigrationKit.Execution;

public sealed record SourceFile(string FileId, string Name, long Length);

public interface IFileUploader
{
    Task UploadAsync(string migrationId, SourceFile file, CancellationToken cancellationToken);
}

public interface IMigrationPlatform
{
    /// <summary>Marks the migration as completed. The platform quarantines files that arrive afterwards.</summary>
    Task CompleteMigrationAsync(string migrationId, CancellationToken cancellationToken);
}

public sealed record FileFailure(string FileId, string Reason);

public sealed record MigrationSummary(string MigrationId, int FilesUploaded, int FilesFailed, bool MarkedComplete)
{
    public IReadOnlyList<FileFailure> Failures { get; init; } = [];
}
