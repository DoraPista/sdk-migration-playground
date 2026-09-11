using System.Runtime.ExceptionServices;
using System.Threading.Channels;

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
        _workers = Math.Max(1, workers);
        _queueCapacity = Math.Max(1, queueCapacity);
    }

    /// <summary>Scans <paramref name="root"/> and uploads everything found. Returns the number of uploaded files.</summary>
    public async Task<int> RunAsync(string root, CancellationToken cancellationToken = default)
    {
        // One token for the whole pipeline: the caller can cancel it, and so can the first failure.
        using var pipelineCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = pipelineCts.Token;
        Exception? firstFailure = null;

        void Fail(Exception ex)
        {
            if (ex is OperationCanceledException && token.IsCancellationRequested)
            {
                return; // a consequence of stopping, not a cause
            }

            Interlocked.CompareExchange(ref firstFailure, ex, null);
            pipelineCts.Cancel();
        }

        // Bounded: the producer waits (asynchronously) when the workers fall behind.
        var channel = Channel.CreateBounded<SourceFile>(new BoundedChannelOptions(_queueCapacity)
        {
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait,
        });
        var uploaded = 0;

        var producer = Task.Run(async () =>
        {
            try
            {
                foreach (var file in _scanner.Scan(root, token))
                {
                    await channel.Writer.WriteAsync(file, token);
                }
            }
            catch (Exception ex)
            {
                Fail(ex);
            }
            finally
            {
                // ALWAYS complete the channel, otherwise consumers wait forever (the original hang).
                channel.Writer.TryComplete();
            }
        });

        var consumers = Enumerable.Range(0, _workers).Select(_ => Task.Run(async () =>
        {
            try
            {
                await foreach (var file in channel.Reader.ReadAllAsync(token))
                {
                    await _uploader.UploadAsync(file, token);
                    Interlocked.Increment(ref uploaded);
                }
            }
            catch (Exception ex)
            {
                Fail(ex);
            }
        })).ToArray();

        await Task.WhenAll(consumers.Append(producer));

        if (firstFailure is not null)
        {
            ExceptionDispatchInfo.Throw(firstFailure); // the root cause, with its original stack trace
        }

        cancellationToken.ThrowIfCancellationRequested();
        return uploaded;
    }
}
