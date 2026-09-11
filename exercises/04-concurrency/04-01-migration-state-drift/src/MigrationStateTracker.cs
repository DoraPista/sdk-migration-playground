namespace MigrationKit.State;

/// <summary>Records which files of a migration have been uploaded, so an interrupted run can resume.</summary>
public sealed class MigrationStateTracker
{
    private readonly IStateStore _store;
    private readonly string _migrationId;

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
        var state = await _store.LoadAsync(_migrationId, cancellationToken);
        if (state.UploadedFileIds.Contains(fileId))
        {
            return;
        }

        state.UploadedFileIds.Add(fileId);
        state.BytesUploaded += bytes;
        state.UpdatedAt = DateTimeOffset.UtcNow;

        await _store.SaveAsync(state, cancellationToken);
        ProgressChanged?.Invoke(this, state.UploadedFileIds.Count);
    }

    public async Task<IReadOnlyCollection<string>> GetUploadedFileIdsAsync(CancellationToken cancellationToken = default)
    {
        var state = await _store.LoadAsync(_migrationId, cancellationToken);
        return state.UploadedFileIds.ToHashSet();
    }
}
