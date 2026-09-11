namespace MigrationKit.Workflow;

public enum MigrationState
{
    NotStarted,
    Authenticating,
    Provisioning,
    Validating,
    Uploading,
    Verifying,
    Completed,
    Failed,
    Cancelled,
}

public sealed record ValidationResult(IReadOnlyList<string> BlockingIssues, IReadOnlyList<string> Warnings)
{
    public static ValidationResult Clean { get; } = new([], []);
}

public sealed record WorkflowResult(MigrationState State, int FilesUploaded, IReadOnlyList<string> FailedFiles, string? Error)
{
    public IReadOnlyList<string> StagesRun { get; init; } = [];

    public string? MigrationId { get; init; }
}

/// <summary>The platform operations a migration needs, in the order the workflow calls them.</summary>
public interface IMigrationPlatform
{
    Task AuthenticateAsync(CancellationToken cancellationToken);

    Task<string> ProvisionAsync(string customerId, CancellationToken cancellationToken);

    Task<string> CreateMigrationAsync(string customerId, string destinationId, CancellationToken cancellationToken);

    Task UploadFileAsync(string migrationId, string path, CancellationToken cancellationToken);

    /// <summary>Asks the platform to check what it received. False means the archive is not intact.</summary>
    Task<bool> VerifyAsync(string migrationId, CancellationToken cancellationToken);

    /// <summary>Marks the migration complete. After this the platform accepts no more files.</summary>
    Task CompleteAsync(string migrationId, CancellationToken cancellationToken);
}

public interface ISourceValidator
{
    Task<ValidationResult> ValidateAsync(IReadOnlyList<string> files, CancellationToken cancellationToken);
}
