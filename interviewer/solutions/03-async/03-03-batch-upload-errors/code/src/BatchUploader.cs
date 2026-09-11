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
        // Turn each upload into a task that cannot fault (except for cancellation), so WhenAll
        // gives us every outcome instead of throwing the first exception and hiding the rest.
        var outcomes = await Task.WhenAll(files.Select(file => UploadOneAsync(file, cancellationToken)));

        cancellationToken.ThrowIfCancellationRequested();
        return new BatchResult(outcomes);
    }

    /// <summary>Used by the "Retry failed" button.</summary>
    public Task<BatchResult> RetryFailedAsync(BatchResult previous, CancellationToken cancellationToken = default) =>
        UploadBatchAsync(previous.FailedFiles, cancellationToken);

    private async Task<FileOutcome> UploadOneAsync(string file, CancellationToken cancellationToken)
    {
        try
        {
            await _transfer.UploadAsync(file, cancellationToken);
            return FileOutcome.Success(file);
        }
        catch (Exception ex) when (!(ex is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            return FileOutcome.Failure(file, ex);
        }
    }
}
