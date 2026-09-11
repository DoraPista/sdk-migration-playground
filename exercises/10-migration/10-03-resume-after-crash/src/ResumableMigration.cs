namespace MigrationKit.Resume;

public sealed class ResumableMigration
{
    private readonly IMigrationPlatform _platform;
    private readonly ICheckpointStore _checkpoints;

    public ResumableMigration(IMigrationPlatform platform, ICheckpointStore checkpoints)
    {
        _platform = platform;
        _checkpoints = checkpoints;
    }

    public async Task<MigrationSummary> RunAsync(string customerId, IReadOnlyList<SourceFile> files, CancellationToken cancellationToken = default)
    {
        var checkpoint = await _checkpoints.LoadAsync(customerId, cancellationToken)
                         ?? new MigrationCheckpoint { CustomerId = customerId, TotalFiles = files.Count };

        var migrationId = await _platform.CreateMigrationAsync(customerId, cancellationToken);
        checkpoint.MigrationId = migrationId;

        var alreadyDone = checkpoint.Files
            .Where(f => f.Status == "Uploaded")
            .Select(f => f.FileId)
            .ToHashSet(StringComparer.Ordinal);

        var uploaded = 0;
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (alreadyDone.Contains(file.FileId))
            {
                continue;
            }

            // Record progress first so the progress bar moves immediately.
            checkpoint.Files.Add(new FileCheckpoint(file.FileId, file.RelativePath, "Uploaded", null));
            checkpoint.LastUpdated = DateTimeOffset.UtcNow;
            await _checkpoints.SaveAsync(checkpoint, cancellationToken);

            await _platform.UploadFileAsync(migrationId, file, cancellationToken);
            uploaded++;
        }

        return new MigrationSummary(migrationId, uploaded, alreadyDone.Count);
    }
}
