using Gym.TestUtilities;
using MigrationKit.Execution;

namespace Ex0201.Execution.Tests;

public sealed class MigrationRunnerTests
{
    [Fact]
    public async Task Small_migration_uploads_every_file_and_completes()
    {
        var uploader = new FakeUploader(async (_, _) => await Task.Yield());
        var platform = new FakePlatform(uploader);
        var runner = new MigrationRunner(uploader, platform, workerCount: 2);

        var summary = await runner.RunAsync("mig-1", Files(3)).WithTimeout();

        Assert.Equal(3, summary.FilesUploaded);
        Assert.True(summary.MarkedComplete);
        Assert.Equal(1, platform.CompleteCalls);
    }

    [Fact]
    public async Task Migration_is_completed_only_after_every_upload_has_finished()
    {
        var gate = new AsyncGate();
        var uploader = new FakeUploader((_, ct) => gate.WaitAsync(ct));
        var platform = new FakePlatform(uploader);
        var runner = new MigrationRunner(uploader, platform, workerCount: 4);

        var run = runner.RunAsync("mig-1", Files(4));
        await gate.WhenWaitingAsync(4);

        // Give an early completion every chance to happen before the uploads are released.
        await Task.WhenAny(platform.Completed, Task.Delay(500));
        gate.Open();
        var summary = await run.WithTimeout();

        Assert.Equal(4, platform.UploadsFinishedWhenCompleted);
        Assert.Equal(4, summary.FilesUploaded);
    }

    [Fact]
    public async Task Failed_upload_is_reported_and_the_migration_is_not_completed()
    {
        var uploader = new FakeUploader(async (file, _) =>
        {
            await Task.Yield();
            if (file.FileId == "F-003")
            {
                throw new IOException(@"The network path \\fs02\projects was not found.");
            }
        });
        var platform = new FakePlatform(uploader);
        var runner = new MigrationRunner(uploader, platform, workerCount: 3);

        var summary = await runner.RunAsync("mig-1", Files(6)).WithTimeout();

        Assert.Equal(1, summary.FilesFailed);
        Assert.Equal(5, summary.FilesUploaded);
        Assert.False(summary.MarkedComplete);
        Assert.Equal(0, platform.CompleteCalls);
    }

    private static IReadOnlyList<SourceFile> Files(int count) =>
        Enumerable.Range(1, count).Select(i => new SourceFile($"F-{i:D3}", $"file-{i}.pdf", 1_000 * i)).ToArray();

    private sealed class FakeUploader(Func<SourceFile, CancellationToken, Task> behaviour) : IFileUploader
    {
        private int _finished;

        public int Finished => Volatile.Read(ref _finished);

        public async Task UploadAsync(string migrationId, SourceFile file, CancellationToken cancellationToken)
        {
            await behaviour(file, cancellationToken);
            Interlocked.Increment(ref _finished);
        }
    }

    private sealed class FakePlatform(FakeUploader uploader) : IMigrationPlatform
    {
        private readonly TaskCompletionSource _completed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Completed => _completed.Task;

        public int CompleteCalls { get; private set; }

        public int UploadsFinishedWhenCompleted { get; private set; } = -1;

        public Task CompleteMigrationAsync(string migrationId, CancellationToken cancellationToken)
        {
            CompleteCalls++;
            UploadsFinishedWhenCompleted = uploader.Finished;
            _completed.TrySetResult();
            return Task.CompletedTask;
        }
    }
}
