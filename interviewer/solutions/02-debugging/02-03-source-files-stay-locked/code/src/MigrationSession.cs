namespace MigrationKit.Transfer;

/// <summary>Uploads the files of one migration and retries failed ones when the network comes back.</summary>
public sealed class MigrationSession : IDisposable
{
    private readonly string _migrationId;
    private readonly IReadOnlyList<string> _files;
    private readonly FileUploader _uploader;
    private readonly INetworkMonitor _networkMonitor;
    private readonly List<string> _pending = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly SemaphoreSlim _retryGate = new(1, 1);
    private int _disposed;

    public MigrationSession(string migrationId, IReadOnlyList<string> files, FileUploader uploader, INetworkMonitor networkMonitor)
    {
        _migrationId = migrationId;
        _files = files;
        _uploader = uploader;
        _networkMonitor = networkMonitor;
        _networkMonitor.ConnectivityChanged += OnConnectivityChanged;
    }

    public IReadOnlyList<string> PendingFiles
    {
        get { lock (_pending) { return _pending.ToArray(); } }
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        foreach (var file in _files)
        {
            try
            {
                await _uploader.UploadAsync(_migrationId, file, linked.Token);
            }
            catch (HttpRequestException)
            {
                lock (_pending) { _pending.Add(file); }
            }
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        // The monitor lives as long as the app. Its event holds a reference to every subscriber,
        // so an un-removed handler keeps the whole session (and its uploader) alive forever.
        _networkMonitor.ConnectivityChanged -= OnConnectivityChanged;
        _lifetime.Cancel();
        _lifetime.Dispose();
    }

    // Event handlers must be async void; so everything inside is guarded (an exception here would crash the process).
    private async void OnConnectivityChanged(object? sender, bool online)
    {
        if (!online || Volatile.Read(ref _disposed) == 1)
        {
            return;
        }

        try
        {
            await RetryPendingAsync(_lifetime.Token);
        }
        catch (OperationCanceledException)
        {
            // Session disposed while retrying.
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task RetryPendingAsync(CancellationToken cancellationToken)
    {
        // Connectivity can flap; don't run two retry passes at once.
        if (!await _retryGate.WaitAsync(0, cancellationToken))
        {
            return;
        }

        try
        {
            foreach (var file in PendingFiles)
            {
                try
                {
                    await _uploader.UploadAsync(_migrationId, file, cancellationToken);
                    lock (_pending) { _pending.Remove(file); }
                }
                catch (HttpRequestException)
                {
                    // Still offline; the next connectivity change will try again.
                }
            }
        }
        finally
        {
            _retryGate.Release();
        }
    }
}
