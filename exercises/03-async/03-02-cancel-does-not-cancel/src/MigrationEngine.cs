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

public sealed class MigrationEngine
{
    private readonly HttpClient _http;
    private readonly MigrationEngineOptions _options;

    public MigrationEngine(HttpClient http, MigrationEngineOptions options)
    {
        _http = http;
        _options = options;
        _http.Timeout = options.RequestTimeout;
    }

    public async Task<EngineResult> RunAsync(string migrationId, IReadOnlyList<string> paths, CancellationToken cancellationToken = default)
    {
        var uploaded = 0;
        var failed = new ConcurrentBag<string>();
        var cancelled = false;

        using var throttle = new SemaphoreSlim(_options.MaxConcurrency);
        var uploads = paths.Select(async path =>
        {
            await throttle.WaitAsync();
            try
            {
                await Task.Run(() => UploadWithRetryAsync(migrationId, path), cancellationToken);
                Interlocked.Increment(ref uploaded);
            }
            catch (TaskCanceledException)
            {
                cancelled = true;
            }
            catch (Exception)
            {
                failed.Add(path);
            }
            finally
            {
                throttle.Release();
            }
        });

        await Task.WhenAll(uploads);

        if (cancelled)
        {
            return new EngineResult(MigrationOutcome.Cancelled, uploaded, failed.ToArray(), "Migration cancelled by user.");
        }

        return failed.IsEmpty
            ? new EngineResult(MigrationOutcome.Completed, uploaded, [], "Migration completed.")
            : new EngineResult(MigrationOutcome.Failed, uploaded, failed.ToArray(), $"{failed.Count} file(s) failed.");
    }

    private async Task UploadWithRetryAsync(string migrationId, string path)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await UploadOnceAsync(migrationId, path);
                return;
            }
            catch (Exception) when (attempt < _options.MaxAttempts)
            {
                await Task.Delay(_options.RetryDelay);
            }
        }
    }

    private async Task UploadOnceAsync(string migrationId, string path)
    {
        await using var file = File.OpenRead(path);
        using var content = new StreamContent(file);
        var name = Uri.EscapeDataString(Path.GetFileName(path));

        using var response = await _http.PostAsync($"migrations/{migrationId}/files?name={name}", content);
        response.EnsureSuccessStatusCode();
    }
}
