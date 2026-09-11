using System.Collections.Concurrent;
using Gym.TestUtilities;
using MigrationKit.Batch;

namespace Ex0301.Batch.Tests;

public sealed class BatchUploaderTests
{
    private static readonly IReadOnlyList<SourceFile> FiveHundredFiles =
        Enumerable.Range(1, 500).Select(i => new SourceFile($"drawing-{i:D3}.dwg", 250_000)).ToArray();

    [Fact]
    public async Task Every_file_is_uploaded_exactly_once()
    {
        var client = new FakeClient();

        await new BatchUploader(client, new UploadOptions()).UploadAllAsync(FiveHundredFiles).WithTimeout();

        Assert.Equal(500, client.Uploaded.Count);
        Assert.Equal(500, client.Uploaded.Distinct().Count());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(16)]
    public async Task Concurrent_uploads_never_exceed_the_configured_limit(int limit)
    {
        var client = new FakeClient();

        await new BatchUploader(client, new UploadOptions { MaxConcurrentUploads = limit }).UploadAllAsync(FiveHundredFiles).WithTimeout();

        Assert.InRange(client.Probe.Peak, 1, limit);
        Assert.Equal(500, client.Uploaded.Count);
    }

    [Fact]
    public async Task Uploads_run_in_parallel_when_the_limit_allows_it()
    {
        var client = new FakeClient();

        await new BatchUploader(client, new UploadOptions { MaxConcurrentUploads = 4 }).UploadAllAsync(FiveHundredFiles.Take(40).ToArray()).WithTimeout();

        Assert.True(client.Probe.Peak > 1, $"Peak concurrency was {client.Probe.Peak}; uploads ran one at a time.");
    }

    [Fact]
    public async Task Progress_reports_every_completed_file()
    {
        var reports = new ConcurrentBag<int>();

        await new BatchUploader(new FakeClient(), new UploadOptions { MaxConcurrentUploads = 4 })
            .UploadAllAsync(FiveHundredFiles, new SyncProgress(reports.Add))
            .WithTimeout();

        Assert.Equal(500, reports.Count);
        Assert.Equal(500, reports.Max());
    }

    [Fact]
    public async Task No_new_uploads_start_after_cancellation()
    {
        using var cts = new CancellationTokenSource();
        var client = new FakeClient(onStarted: started =>
        {
            if (started == 10)
            {
                cts.Cancel();
            }
        });
        const int limit = 4;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new BatchUploader(client, new UploadOptions { MaxConcurrentUploads = limit }).UploadAllAsync(FiveHundredFiles, cancellationToken: cts.Token).WithTimeout());

        Assert.InRange(client.Probe.Total, 10, 10 + limit);
    }

    private sealed class FakeClient(Action<int>? onStarted = null) : IUploadClient
    {
        public ConcurrencyProbe Probe { get; } = new();

        public ConcurrentBag<string> Uploaded { get; } = new();

        public async Task UploadAsync(SourceFile file, CancellationToken cancellationToken)
        {
            using (Probe.Enter())
            {
                onStarted?.Invoke(Probe.Total);
                await Task.Delay(1, cancellationToken);
                Uploaded.Add(file.Name);
            }
        }
    }

    /// <summary>IProgress that reports inline (Progress&lt;T&gt; would post to the thread pool).</summary>
    private sealed class SyncProgress(Action<int> report) : IProgress<int>
    {
        public void Report(int value) => report(value);
    }
}
