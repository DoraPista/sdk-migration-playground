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
        // 1. Which migration are we continuing?
        //    The local checkpoint is a hint; if it is missing or unreadable, ask the platform.
        var checkpoint = await LoadCheckpointAsync(customerId, cancellationToken);
        var migrationId = checkpoint?.MigrationId is { Length: > 0 } known
            ? known
            : await _platform.FindOpenMigrationAsync(customerId, cancellationToken).ConfigureAwait(false)
              ?? await _platform.CreateMigrationAsync(customerId, cancellationToken).ConfigureAwait(false);

        checkpoint ??= new MigrationCheckpoint { CustomerId = customerId, TotalFiles = files.Count };
        checkpoint.MigrationId = migrationId;
        checkpoint.TotalFiles = files.Count;

        // 2. What does the platform ALREADY have? The platform is the source of truth: the local checkpoint
        //    can be stale (crash before it was written) or optimistic (written before the upload succeeded).
        var stored = new HashSet<string>(
            await _platform.ListUploadedFileIdsAsync(migrationId, cancellationToken).ConfigureAwait(false),
            StringComparer.Ordinal);

        // The checkpoint is rebuilt from that truth, so a "phantom" entry cannot survive a resume.
        checkpoint.Files = checkpoint.Files.Where(f => stored.Contains(f.FileId)).ToList();

        var uploaded = 0;
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (stored.Contains(file.FileId))
            {
                continue;
            }

            // 3. Upload FIRST, record afterwards. A crash then costs one repeated upload attempt
            //    (the platform de-duplicates, see 07-03), never a silently skipped file.
            var remoteFileId = await _platform.UploadFileAsync(migrationId, file, cancellationToken).ConfigureAwait(false);

            checkpoint.Files.Add(new FileCheckpoint(file.FileId, file.RelativePath, "Uploaded", remoteFileId));
            checkpoint.LastUpdated = DateTimeOffset.UtcNow;
            await _checkpoints.SaveAsync(checkpoint, cancellationToken).ConfigureAwait(false);
            uploaded++;
        }

        return new MigrationSummary(migrationId, uploaded, stored.Count);
    }

    /// <summary>A checkpoint we cannot read is not an error: it just means we have to ask the platform.</summary>
    private async Task<MigrationCheckpoint?> LoadCheckpointAsync(string customerId, CancellationToken cancellationToken)
    {
        try
        {
            return await _checkpoints.LoadAsync(customerId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidDataException)
        {
            return null;
        }
    }
}
