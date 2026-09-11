using System.Collections.Concurrent;
using System.Net;
using Gym.TestUtilities;
using MigrationKit.Metadata;

namespace Ex0303.Batch.Tests;

public sealed class BatchUploaderTests
{
    [Fact]
    public async Task Successful_batch_reports_every_file_as_succeeded()
    {
        var transfer = new FakeTransfer();

        var result = await new BatchUploader(transfer).UploadBatchAsync(["a.json", "b.json", "c.json"]);

        Assert.True(result.AllSucceeded);
        Assert.Equal(3, result.Outcomes.Count);
    }

    [Fact]
    public async Task Each_file_gets_its_own_outcome_and_error()
    {
        var transfer = new FakeTransfer
        {
            ["b.json"] = () => throw new HttpRequestException("Server error", null, HttpStatusCode.InternalServerError),
            ["c.json"] = () => throw new InvalidDataException("Field 'capturedAt' is not a valid date."),
        };

        var result = await new BatchUploader(transfer).UploadBatchAsync(["a.json", "b.json", "c.json"]);

        Assert.True(Outcome(result, "a.json").Succeeded);
        Assert.IsType<HttpRequestException>(Outcome(result, "b.json").Error);
        Assert.IsType<InvalidDataException>(Outcome(result, "c.json").Error);
        Assert.Equal(["b.json", "c.json"], result.FailedFiles.Order());
    }

    [Fact]
    public async Task Retry_failed_resends_only_the_files_that_failed()
    {
        var failOnce = true;
        var transfer = new FakeTransfer
        {
            ["b.json"] = () =>
            {
                if (failOnce)
                {
                    failOnce = false;
                    throw new HttpRequestException("Server error", null, HttpStatusCode.ServiceUnavailable);
                }
            },
        };
        var uploader = new BatchUploader(transfer);

        var first = await uploader.UploadBatchAsync(["a.json", "b.json", "c.json"]);
        var retry = await uploader.RetryFailedAsync(first);

        Assert.True(retry.AllSucceeded);
        Assert.Equal(1, transfer.Calls("a.json"));
        Assert.Equal(1, transfer.Calls("c.json"));
        Assert.Equal(2, transfer.Calls("b.json"));
    }

    [Fact]
    public async Task Batch_finishes_only_when_every_upload_has_finished()
    {
        var gate = new AsyncGate();
        var transfer = new FakeTransfer { AsyncBehaviour = async (file, ct) =>
        {
            if (file == "fast-failure.json") throw new HttpRequestException("boom");
            await gate.WaitAsync(ct);
        } };

        var batch = new BatchUploader(transfer).UploadBatchAsync(["fast-failure.json", "slow.json"]);
        await gate.WhenWaitingAsync(1);
        await Task.Delay(100);

        Assert.False(batch.IsCompleted);
        gate.Open();
        var result = await batch.WithTimeout();
        Assert.True(Outcome(result, "slow.json").Succeeded);
    }

    [Fact]
    public async Task Cancellation_is_not_reported_as_a_file_failure()
    {
        using var cts = new CancellationTokenSource();
        var transfer = new FakeTransfer { AsyncBehaviour = async (_, ct) => await Task.Delay(Timeout.Infinite, ct) };

        var batch = new BatchUploader(transfer).UploadBatchAsync(["a.json", "b.json"], cts.Token);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => batch.WithTimeout());
    }

    private static FileOutcome Outcome(BatchResult result, string file) => Assert.Single(result.Outcomes, o => o.FileName == file);

    private sealed class FakeTransfer : Dictionary<string, Action>, IFileTransfer
    {
        private readonly ConcurrentDictionary<string, int> _calls = new();

        public Func<string, CancellationToken, Task>? AsyncBehaviour { get; init; }

        public int Calls(string file) => _calls.GetValueOrDefault(file);

        public async Task UploadAsync(string fileName, CancellationToken cancellationToken)
        {
            _calls.AddOrUpdate(fileName, 1, (_, n) => n + 1);
            await Task.Yield();
            if (AsyncBehaviour is not null)
            {
                await AsyncBehaviour(fileName, cancellationToken);
            }

            if (TryGetValue(fileName, out var behaviour))
            {
                behaviour();
            }
        }
    }
}
