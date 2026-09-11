using System.Collections.Concurrent;

namespace MigrationKit.Execution;

/// <summary>Uploads the files of a migration in parallel, then completes the migration on the platform.</summary>
public sealed class MigrationRunner
{
    private readonly IFileUploader _uploader;
    private readonly IMigrationPlatform _platform;
    private readonly int _workerCount;

    public MigrationRunner(IFileUploader uploader, IMigrationPlatform platform, int workerCount = 4)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(workerCount, 1);
        _uploader = uploader;
        _platform = platform;
        _workerCount = workerCount;
    }

    /// <summary>Raised after each file has been uploaded (used by the UI to show progress).</summary>
    public event EventHandler<SourceFile>? FileUploaded;

    public async Task<MigrationSummary> RunAsync(string migrationId, IReadOnlyList<SourceFile> files, CancellationToken cancellationToken = default)
    {
        // Per-run state: the runner can be reused and runs can't interfere with each other.
        var queue = new ConcurrentQueue<SourceFile>(files);
        var failures = new ConcurrentBag<FileFailure>();
        var uploaded = 0;

        async Task WorkerAsync()
        {
            while (queue.TryDequeue(out var file))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    await _uploader.UploadAsync(migrationId, file, cancellationToken);
                }
                catch (Exception ex) when (!(ex is OperationCanceledException && cancellationToken.IsCancellationRequested))
                {
                    // One bad file must not stop the others, but it must be reported.
                    failures.Add(new FileFailure(file.FileId, ex.Message));
                    continue;
                }

                Interlocked.Increment(ref uploaded);
                FileUploaded?.Invoke(this, file);
            }
        }

        // Keep the worker tasks and await them: "the queue is empty" is not "the work is done".
        var workers = Enumerable.Range(0, Math.Min(_workerCount, Math.Max(files.Count, 1)))
            .Select(_ => Task.Run(WorkerAsync, cancellationToken))
            .ToArray();
        await Task.WhenAll(workers);

        var allUploaded = failures.IsEmpty;
        if (allUploaded)
        {
            await _platform.CompleteMigrationAsync(migrationId, cancellationToken);
        }

        return new MigrationSummary(migrationId, uploaded, failures.Count, MarkedComplete: allUploaded)
        {
            Failures = failures.OrderBy(f => f.FileId, StringComparer.Ordinal).ToArray(),
        };
    }
}
