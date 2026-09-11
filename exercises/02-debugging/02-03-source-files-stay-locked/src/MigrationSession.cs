namespace MigrationKit.Transfer;

/// <summary>Uploads the files of one migration and retries failed ones when the network comes back.</summary>
public sealed class MigrationSession : IDisposable
{
    private readonly string _migrationId;
    private readonly IReadOnlyList<string> _files;
    private readonly FileUploader _uploader;
    private readonly List<string> _pending = new();
    private bool _disposed;

    public MigrationSession(string migrationId, IReadOnlyList<string> files, FileUploader uploader, INetworkMonitor networkMonitor)
    {
        _migrationId = migrationId;
        _files = files;
        _uploader = uploader;
        networkMonitor.ConnectivityChanged += OnConnectivityChanged;
    }

    public IReadOnlyList<string> PendingFiles => _pending;

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        foreach (var file in _files)
        {
            try
            {
                await _uploader.UploadAsync(_migrationId, file, cancellationToken);
            }
            catch (HttpRequestException)
            {
                _pending.Add(file);
            }
        }
    }

    public void Dispose()
    {
        _disposed = true;
    }

    private async void OnConnectivityChanged(object? sender, bool online)
    {
        if (!online || _pending.Count == 0)
        {
            return;
        }

        foreach (var file in _pending.ToList())
        {
            try
            {
                await _uploader.UploadAsync(_migrationId, file);
                _pending.Remove(file);
            }
            catch (HttpRequestException)
            {
                // Still offline; we'll try again on the next connectivity change.
            }
        }
    }
}
