using System.Collections.Concurrent;

namespace MigrationKit.Pipeline;

public sealed record SourceFile(string Path, long Length);

public interface ISourceScanner
{
    /// <summary>Lazily enumerates a (possibly huge, possibly slow) share.</summary>
    IEnumerable<SourceFile> Scan(string root, CancellationToken cancellationToken);
}

public interface IFileUploader
{
    Task UploadAsync(SourceFile file, CancellationToken cancellationToken);
}

public sealed class ScanAndUploadPipeline
{
    private readonly ISourceScanner _scanner;
    private readonly IFileUploader _uploader;
    private readonly int _workers;
    private readonly int _queueCapacity;

    public ScanAndUploadPipeline(ISourceScanner scanner, IFileUploader uploader, int workers = 4, int queueCapacity = 100)
    {
        _scanner = scanner;
        _uploader = uploader;
        _workers = workers;
        _queueCapacity = queueCapacity;
    }

    /// <summary>Scans <paramref name="root"/> and uploads everything found. Returns the number of uploaded files.</summary>
    public async Task<int> RunAsync(string root, CancellationToken cancellationToken = default)
    {
        using var queue = new BlockingCollection<SourceFile>(_queueCapacity);
        var uploaded = 0;

        var producer = Task.Run(() =>
        {
            foreach (var file in _scanner.Scan(root, cancellationToken))
            {
                queue.Add(file);
            }

            queue.CompleteAdding();
        });

        var consumers = Enumerable.Range(0, _workers).Select(_ => Task.Run(async () =>
        {
            foreach (var file in queue.GetConsumingEnumerable())
            {
                await _uploader.UploadAsync(file, cancellationToken);
                Interlocked.Increment(ref uploaded);
            }
        })).ToArray();

        await Task.WhenAll(consumers);
        return uploaded;
    }
}
