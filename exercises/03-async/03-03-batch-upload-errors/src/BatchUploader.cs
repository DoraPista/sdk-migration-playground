namespace MigrationKit.Metadata;

public interface IFileTransfer
{
    Task UploadAsync(string fileName, CancellationToken cancellationToken);
}

public sealed record FileOutcome(string FileName, bool Succeeded, Exception? Error)
{
    public static FileOutcome Success(string fileName) => new(fileName, true, null);

    public static FileOutcome Failure(string fileName, Exception error) => new(fileName, false, error);
}

public sealed record BatchResult(IReadOnlyList<FileOutcome> Outcomes)
{
    public bool AllSucceeded => Outcomes.All(o => o.Succeeded);

    public IReadOnlyList<string> FailedFiles => Outcomes.Where(o => !o.Succeeded).Select(o => o.FileName).ToArray();
}

public sealed class BatchUploader
{
    private readonly IFileTransfer _transfer;

    public BatchUploader(IFileTransfer transfer)
    {
        _transfer = transfer;
    }

    public async Task<BatchResult> UploadBatchAsync(IReadOnlyList<string> files, CancellationToken cancellationToken = default)
    {
        try
        {
            await Task.WhenAll(files.Select(file => _transfer.UploadAsync(file, cancellationToken)));
            return new BatchResult(files.Select(FileOutcome.Success).ToArray());
        }
        catch (Exception ex)
        {
            return new BatchResult(files.Select(file => FileOutcome.Failure(file, ex)).ToArray());
        }
    }

    /// <summary>Used by the "Retry failed" button.</summary>
    public Task<BatchResult> RetryFailedAsync(BatchResult previous, CancellationToken cancellationToken = default) =>
        UploadBatchAsync(previous.FailedFiles, cancellationToken);
}
