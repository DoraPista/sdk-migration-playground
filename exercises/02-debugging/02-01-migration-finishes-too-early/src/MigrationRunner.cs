using System.Collections.Concurrent;

namespace MigrationKit.Execution;

/// <summary>Uploads the files of a migration in parallel, then completes the migration on the platform.</summary>
public sealed class MigrationRunner
{
    private readonly IFileUploader _uploader;
    private readonly IMigrationPlatform _platform;
    private readonly int _workerCount;
    private readonly ConcurrentQueue<SourceFile> _queue = new();
    private int _uploaded;
    private int _failed;

    public MigrationRunner(IFileUploader uploader, IMigrationPlatform platform, int workerCount = 4)
    {
        _uploader = uploader;
        _platform = platform;
        _workerCount = workerCount;
    }

    /// <summary>Raised after each file has been uploaded (used by the UI to show progress).</summary>
    public event EventHandler<SourceFile>? FileUploaded;

    public async Task<MigrationSummary> RunAsync(string migrationId, IReadOnlyList<SourceFile> files, CancellationToken cancellationToken = default)
    {
        foreach (var file in files)
        {
            _queue.Enqueue(file);
        }

        for (var i = 0; i < _workerCount; i++)
        {
            _ = Task.Run(() => WorkerAsync(migrationId, cancellationToken), cancellationToken);
        }

        // Wait until the workers have picked up every file.
        while (!_queue.IsEmpty)
        {
            await Task.Delay(50, cancellationToken);
        }

        await _platform.CompleteMigrationAsync(migrationId, cancellationToken);

        return new MigrationSummary(migrationId, _uploaded, _failed, MarkedComplete: true);
    }

    private async Task WorkerAsync(string migrationId, CancellationToken cancellationToken)
    {
        while (_queue.TryDequeue(out var file))
        {
            await _uploader.UploadAsync(migrationId, file, cancellationToken);
            _uploaded++;
            FileUploaded?.Invoke(this, file);
        }
    }
}
