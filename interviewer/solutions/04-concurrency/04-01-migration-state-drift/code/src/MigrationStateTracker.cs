namespace MigrationKit.State;

/// <summary>Records which files of a migration have been uploaded, so an interrupted run can resume.</summary>
/// <remarks>
/// The tracker is the only writer of this migration's state. Every read-modify-write runs under an
/// async lock, so an update can't be based on a stale copy, and a save can't overwrite a newer one.
/// </remarks>
public sealed class MigrationStateTracker
{
    private readonly IStateStore _store;
    private readonly string _migrationId;
    private readonly SemaphoreSlim _mutex = new(1, 1);

    public MigrationStateTracker(IStateStore store, string migrationId)
    {
        _store = store;
        _migrationId = migrationId;
    }

    /// <summary>Raised with the number of uploaded files after each change.</summary>
    public event EventHandler<int>? ProgressChanged;

    /// <summary>Called by the upload workers, possibly several at the same time.</summary>
    public async Task MarkUploadedAsync(string fileId, long bytes, CancellationToken cancellationToken = default)
    {
        int uploadedCount;

        // A C# lock can't contain an await; SemaphoreSlim(1,1) is the async equivalent.
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var state = await _store.LoadAsync(_migrationId, cancellationToken);
            if (state.UploadedFileIds.Contains(fileId))
            {
                return;
            }

            state.UploadedFileIds.Add(fileId);
            state.BytesUploaded += bytes;
            state.UpdatedAt = DateTimeOffset.UtcNow;
            await _store.SaveAsync(state, cancellationToken);
            uploadedCount = state.UploadedFileIds.Count;

            // Raised inside the lock so notifications are ordered like the updates they describe.
            // Subscribers must be quick (the UI should marshal, not block).
            ProgressChanged?.Invoke(this, uploadedCount);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<IReadOnlyCollection<string>> GetUploadedFileIdsAsync(CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var state = await _store.LoadAsync(_migrationId, cancellationToken);
            return state.UploadedFileIds.ToHashSet();
        }
        finally
        {
            _mutex.Release();
        }
    }
}
