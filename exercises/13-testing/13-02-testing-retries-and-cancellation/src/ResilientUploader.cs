namespace MigrationKit.Resilient;

public sealed record UploadRequest(string FileName, byte[] Content);

public sealed record UploadReceipt(string FileId, int Attempts);

/// <summary>The platform's upload API.</summary>
public interface IUploadApi
{
    /// <summary>Opens an upload session. Sessions left open cost the customer storage.</summary>
    Task<string> StartSessionAsync(string migrationId, UploadRequest request, CancellationToken cancellationToken);

    /// <summary>Sends the content. Throws <see cref="HttpRequestException"/> for transient problems.</summary>
    Task<string> SendAsync(string sessionId, byte[] content, CancellationToken cancellationToken);

    /// <summary>Tells the platform to discard the session and everything in it.</summary>
    Task AbortSessionAsync(string sessionId, CancellationToken cancellationToken);
}

public sealed class ResilientUploader
{
    private const int MaxAttempts = 4;

    private readonly IUploadApi _api;

    public ResilientUploader(IUploadApi api)
    {
        _api = api;
    }

    public async Task<UploadReceipt> UploadAsync(string migrationId, UploadRequest request, CancellationToken cancellationToken = default)
    {
        var sessionId = await _api.StartSessionAsync(migrationId, request, cancellationToken);

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var fileId = await _api.SendAsync(sessionId, request.Content, cancellationToken);
                return new UploadReceipt(fileId, attempt);
            }
            catch (HttpRequestException) when (attempt < MaxAttempts)
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // The user cancelled: let the platform release the session.
                await _api.AbortSessionAsync(sessionId, cancellationToken);
                throw;
            }
        }
    }
}
