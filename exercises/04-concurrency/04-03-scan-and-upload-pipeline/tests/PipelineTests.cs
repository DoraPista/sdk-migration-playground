using Gym.TestUtilities;
using MigrationKit.Pipeline;

namespace Ex0403.Pipeline.Tests;

public sealed class PipelineTests
{
    private static readonly TimeSpan Prompt = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Everything_found_is_uploaded()
    {
        var uploader = new FakeUploader();

        var uploaded = await new ScanAndUploadPipeline(new FakeScanner(1_000), uploader).RunAsync("\\\\fs01\\projects").WithTimeout(Prompt);

        Assert.Equal(1_000, uploaded);
        Assert.Equal(1_000, uploader.Uploaded);
    }

    [Fact]
    public async Task Scanner_stays_a_bounded_distance_ahead_of_slow_uploads()
    {
        var scanner = new FakeScanner(10_000);
        var gate = new AsyncGate();
        var uploader = new FakeUploader(gate);
        var pipeline = new ScanAndUploadPipeline(scanner, uploader, workers: 4, queueCapacity: 50);

        var run = pipeline.RunAsync("\\\\fs01\\projects");
        await gate.WhenWaitingAsync(4);
        await Task.Delay(200); // plenty of time for an unbounded scanner to race ahead

        Assert.InRange(scanner.Produced, 4, 50 + 4 + 2);
        gate.Open();
        Assert.Equal(10_000, await run.WithTimeout(Prompt));
    }

    [Fact]
    public async Task Scanner_failure_ends_the_run_with_the_scanner_error()
    {
        var scanner = new FakeScanner(1_000, failAfter: 10);

        var error = await Assert.ThrowsAnyAsync<Exception>(() =>
            new ScanAndUploadPipeline(scanner, new FakeUploader()).RunAsync("\\\\fs01\\projects").WithTimeout(Prompt));

        Assert.IsType<IOException>(error);
    }

    [Fact]
    public async Task Cancellation_ends_the_run_promptly_while_the_scan_is_still_running()
    {
        var scanner = new FakeScanner(1_000_000, stallAfter: 3);
        var uploader = new FakeUploader();
        using var cts = new CancellationTokenSource();

        var run = new ScanAndUploadPipeline(scanner, uploader).RunAsync("\\\\fs01\\projects", cts.Token);
        await Eventually.TrueAsync(() => uploader.Uploaded == 3, Prompt, "the first files were uploaded");
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run.WithTimeout(Prompt));
    }

    [Fact]
    public async Task Upload_failure_ends_the_run_and_stops_the_scan()
    {
        var scanner = new FakeScanner(100_000);
        var uploader = new FakeUploader(failOn: 7);

        var error = await Assert.ThrowsAnyAsync<Exception>(() =>
            new ScanAndUploadPipeline(scanner, uploader, workers: 4, queueCapacity: 20).RunAsync("\\\\fs01\\projects").WithTimeout(Prompt));

        Assert.IsType<HttpRequestException>(error);
        await Task.Delay(100);
        Assert.True(scanner.Produced < 1_000, $"The scanner kept going after the failure ({scanner.Produced} files produced).");
    }

    /// <summary>Enumerates synthetic files; can fail or stall (like a slow share) part-way through.</summary>
    private sealed class FakeScanner(int count, int? failAfter = null, int? stallAfter = null) : ISourceScanner
    {
        private int _produced;

        public int Produced => Volatile.Read(ref _produced);

        public IEnumerable<SourceFile> Scan(string root, CancellationToken cancellationToken)
        {
            for (var i = 1; i <= count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (i > failAfter)
                {
                    throw new IOException($@"The specified network name is no longer available: {root}\Archive\1998");
                }

                while (i > stallAfter)
                {
                    // Enumerating a folder with a million entries over a VPN.
                    cancellationToken.ThrowIfCancellationRequested();
                    Thread.Sleep(10);
                }

                Interlocked.Increment(ref _produced);
                yield return new SourceFile($@"{root}\file-{i:D6}.pdf", 1_000);
            }
        }
    }

    private sealed class FakeUploader(AsyncGate? gate = null, int? failOn = null) : IFileUploader
    {
        private int _started;
        private int _uploaded;

        public int Uploaded => Volatile.Read(ref _uploaded);

        public async Task UploadAsync(SourceFile file, CancellationToken cancellationToken)
        {
            var number = Interlocked.Increment(ref _started);
            if (gate is not null)
            {
                await gate.WaitAsync(cancellationToken);
            }

            await Task.Yield();
            if (number == failOn)
            {
                throw new HttpRequestException("403 Forbidden: the customer's subscription has expired.");
            }

            Interlocked.Increment(ref _uploaded);
        }
    }
}
