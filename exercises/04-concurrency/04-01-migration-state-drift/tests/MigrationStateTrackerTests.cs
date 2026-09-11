using System.Collections.Concurrent;
using System.Text.Json;
using Gym.TestUtilities;
using MigrationKit.State;

namespace Ex0401.State.Tests;

public sealed class MigrationStateTrackerTests : IDisposable
{
    private readonly TestFiles _files = new();

    [Fact]
    public async Task Uploaded_files_are_recorded()
    {
        var tracker = new MigrationStateTracker(new SlowStore(), "mig-1");

        await tracker.MarkUploadedAsync("F-1", 100);
        await tracker.MarkUploadedAsync("F-2", 200);
        await tracker.MarkUploadedAsync("F-3", 300);

        Assert.Equal(["F-1", "F-2", "F-3"], (await tracker.GetUploadedFileIdsAsync()).Order());
    }

    [Fact]
    public async Task A_file_reported_twice_is_counted_once()
    {
        var store = new SlowStore();
        var tracker = new MigrationStateTracker(store, "mig-1");

        await tracker.MarkUploadedAsync("F-1", 100);
        await tracker.MarkUploadedAsync("F-1", 100);

        Assert.Equal(100, store.Persisted.BytesUploaded);
    }

    [Fact]
    public async Task State_survives_a_restart_with_the_file_store()
    {
        var first = new MigrationStateTracker(new JsonFileStateStore(_files.Root), "mig-1");
        await first.MarkUploadedAsync("F-1", 100);
        await first.MarkUploadedAsync("F-2", 200);

        var afterRestart = new MigrationStateTracker(new JsonFileStateStore(_files.Root), "mig-1");

        Assert.Equal(2, (await afterRestart.GetUploadedFileIdsAsync()).Count);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(16)]
    public async Task Concurrent_updates_are_never_lost(int workers)
    {
        var store = new SlowStore();
        var tracker = new MigrationStateTracker(store, "mig-1");

        // All workers finish an upload at (nearly) the same moment.
        await Task.WhenAll(Enumerable.Range(1, workers).Select(i => tracker.MarkUploadedAsync($"F-{i}", 1_000))).WithTimeout();

        Assert.Equal(workers, store.Persisted.UploadedFileIds.Count);
        Assert.Equal(workers * 1_000L, store.Persisted.BytesUploaded);
    }

    [Fact]
    public async Task Progress_never_goes_backwards()
    {
        var tracker = new MigrationStateTracker(new SlowStore(), "mig-1");
        var reports = new ConcurrentQueue<int>();
        tracker.ProgressChanged += (_, count) => reports.Enqueue(count);

        await Task.WhenAll(Enumerable.Range(1, 12).Select(i => tracker.MarkUploadedAsync($"F-{i}", 10))).WithTimeout();

        var sequence = reports.ToArray();
        Assert.Equal(12, sequence.Max());
        Assert.True(sequence.SequenceEqual(sequence.Order()), $"Progress went backwards: {string.Join(", ", sequence)}");
    }

    public void Dispose() => _files.Dispose();

    /// <summary>
    /// Behaves like durable storage: every load returns a fresh copy of what was last saved,
    /// and every call takes a little while (disk or network share latency).
    /// </summary>
    private sealed class SlowStore : IStateStore
    {
        private string _json = JsonSerializer.Serialize(new MigrationProgressState { MigrationId = "mig-1" });

        public MigrationProgressState Persisted => JsonSerializer.Deserialize<MigrationProgressState>(Volatile.Read(ref _json))!;

        public async Task<MigrationProgressState> LoadAsync(string migrationId, CancellationToken cancellationToken)
        {
            var snapshot = Persisted;
            await Task.Delay(15, cancellationToken);
            return snapshot;
        }

        public async Task SaveAsync(MigrationProgressState state, CancellationToken cancellationToken)
        {
            var json = JsonSerializer.Serialize(state);
            await Task.Delay(5, cancellationToken);
            Volatile.Write(ref _json, json);
        }
    }
}
