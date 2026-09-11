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

    /// <summary>How long the abort is given once the caller's own token is already cancelled.</summary>
    private static readonly TimeSpan AbortTimeout = TimeSpan.FromSeconds(10);

    private readonly IUploadApi _api;
    private readonly TimeProvider _time;

    // The default keeps every existing `new ResilientUploader(api)` working.
    public ResilientUploader(IUploadApi api, TimeProvider? time = null)
    {
        _api = api;
        _time = time ?? TimeProvider.System;
    }

    public async Task<UploadReceipt> UploadAsync(string migrationId, UploadRequest request, CancellationToken cancellationToken = default)
    {
        var sessionId = await _api.StartSessionAsync(migrationId, request, cancellationToken).ConfigureAwait(false);

        try
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    var fileId = await _api.SendAsync(sessionId, request.Content, cancellationToken).ConfigureAwait(false);
                    return new UploadReceipt(fileId, attempt);
                }
                catch (HttpRequestException) when (attempt < MaxAttempts)
                {
                    // Through TimeProvider, so a test can move the clock instead of waiting 15 seconds.
                    await Task.Delay(BackoffFor(attempt), _time, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or HttpRequestException)
        {
            // However the upload ended - cancelled mid-send, cancelled during the backoff, or out of
            // attempts - the session must not be left open on the platform.
            await AbortQuietlyAsync(sessionId).ConfigureAwait(false);
            throw;
        }
    }

    private static TimeSpan BackoffFor(int attempt) => TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));

    /// <summary>
    /// Best-effort clean-up. It deliberately does NOT use the caller's token: that token has usually just
    /// been cancelled, and passing it would cancel the abort before it left the machine - which is exactly
    /// how 4,000 sessions were abandoned. It must also never replace the original failure with its own.
    /// </summary>
    private async Task AbortQuietlyAsync(string sessionId)
    {
        using var abortTimeout = new CancellationTokenSource(AbortTimeout, _time);

        try
        {
            await _api.AbortSessionAsync(sessionId, abortTimeout.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or ObjectDisposedException)
        {
            // Nothing useful to do; the platform expires sessions eventually. A real SDK logs this.
        }
    }
}
