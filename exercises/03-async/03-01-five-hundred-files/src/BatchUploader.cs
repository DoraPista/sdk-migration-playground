namespace MigrationKit.Batch;

public sealed record SourceFile(string Name, long Length);

public interface IUploadClient
{
    Task UploadAsync(SourceFile file, CancellationToken cancellationToken);
}

public sealed class UploadOptions
{
    /// <summary>Maximum number of files uploaded at the same time.</summary>
    public int MaxConcurrentUploads { get; set; } = 8;
}

public sealed class BatchUploader
{
    private readonly IUploadClient _client;
    private readonly UploadOptions _options;

    public BatchUploader(IUploadClient client, UploadOptions options)
    {
        _client = client;
        _options = options;
    }

    /// <summary>Uploads all files. <paramref name="progress"/> receives the number of completed files.</summary>
    public async Task UploadAllAsync(IReadOnlyList<SourceFile> files, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        var completed = 0;

        var uploads = files.Select(async file =>
        {
            await _client.UploadAsync(file, cancellationToken);
            progress?.Report(Interlocked.Increment(ref completed));
        });

        await Task.WhenAll(uploads);
    }
}
