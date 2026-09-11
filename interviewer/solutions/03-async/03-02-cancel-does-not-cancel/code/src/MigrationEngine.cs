using System.Collections.Concurrent;

namespace MigrationKit.Engine;

public sealed class MigrationEngineOptions
{
    public int MaxConcurrency { get; set; } = 4;

    /// <summary>Time limit for a single HTTP attempt.</summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(100);

    public int MaxAttempts { get; set; } = 3;

    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(2);
}

public enum MigrationOutcome
{
    Completed,
    Failed,
    Cancelled,
}

public sealed record EngineResult(MigrationOutcome Outcome, int FilesUploaded, IReadOnlyList<string> FailedFiles, string Message);

/// <summary>Thrown for an attempt that exceeded <see cref="MigrationEngineOptions.RequestTimeout"/>.</summary>
public sealed class AttemptTimedOutException(TimeSpan timeout, Exception inner)
    : TimeoutException($"The upload attempt did not complete within {timeout.TotalSeconds:0.#}s.", inner);

public sealed class MigrationEngine
{
    private readonly HttpClient _http;
    private readonly MigrationEngineOptions _options;

    public MigrationEngine(HttpClient http, MigrationEngineOptions options)
    {
        _http = http;
        _options = options;
        // Don't mutate the (possibly shared) HttpClient. The per-attempt timeout is enforced below, where
        // it can be told apart from user cancellation.
    }

    public async Task<EngineResult> RunAsync(string migrationId, IReadOnlyList<string> paths, CancellationToken cancellationToken = default)
    {
        var uploaded = 0;
        var failed = new ConcurrentBag<string>();

        try
        {
            await Parallel.ForEachAsync(
                paths,
                new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, _options.MaxConcurrency), CancellationToken = cancellationToken },
                async (path, ct) =>
                {
                    try
                    {
                        await UploadWithRetryAsync(migrationId, path, ct);
                        Interlocked.Increment(ref uploaded);
                    }
                    catch (Exception ex) when (!IsCancellation(ex, ct))
                    {
                        failed.Add(path);
                    }
                });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The ONLY place where "cancelled" is decided: the caller's token was cancelled.
            return new EngineResult(MigrationOutcome.Cancelled, uploaded, failed.ToArray(), "Migration cancelled by user.");
        }

        return failed.IsEmpty
            ? new EngineResult(MigrationOutcome.Completed, uploaded, [], "Migration completed.")
            : new EngineResult(MigrationOutcome.Failed, uploaded, failed.ToArray(), $"{failed.Count} file(s) failed.");
    }

    private async Task UploadWithRetryAsync(string migrationId, string path, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await UploadOnceAsync(migrationId, path, cancellationToken);
                return;
            }
            catch (Exception ex) when (attempt < _options.MaxAttempts && !IsCancellation(ex, cancellationToken))
            {
                // The delay observes the token: cancelling during a back-off returns immediately.
                await Task.Delay(_options.RetryDelay, cancellationToken);
            }
        }
    }

    private async Task UploadOnceAsync(string migrationId, string path, CancellationToken cancellationToken)
    {
        using var attemptTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        attemptTimeout.CancelAfter(_options.RequestTimeout);

        try
        {
            await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
            using var content = new StreamContent(file);
            var name = Uri.EscapeDataString(Path.GetFileName(path));

            using var response = await _http.PostAsync($"migrations/{migrationId}/files?name={name}", content, attemptTimeout.Token);
            response.EnsureSuccessStatusCode();
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Our timeout fired, not the caller's token: this is a transient failure, not a cancellation.
            throw new AttemptTimedOutException(_options.RequestTimeout, ex);
        }
    }

    private static bool IsCancellation(Exception ex, CancellationToken token) =>
        ex is OperationCanceledException && token.IsCancellationRequested;
}
