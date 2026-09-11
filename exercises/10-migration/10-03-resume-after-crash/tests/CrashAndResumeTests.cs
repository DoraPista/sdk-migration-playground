using System.Collections.Concurrent;
using Gym.TestUtilities;
using MigrationKit.Resume;

namespace Ex1003.Resume.Tests;

public sealed class CrashAndResumeTests : IDisposable
{
    private const string Customer = "CUST-1001";

    private static readonly IReadOnlyList<SourceFile> Files = Enumerable.Range(1, 10)
        .Select(i => new SourceFile($"F-{i:D3}", $"documents/file-{i:D2}.pdf", i * 1_000))
        .ToArray();

    private readonly TestFiles _appData = new();
    private readonly FakePlatform _platform = new();
    private readonly JsonCheckpointStore _checkpoints;

    public CrashAndResumeTests() => _checkpoints = new JsonCheckpointStore(_appData.Root);

    [Fact]
    public async Task Uninterrupted_migration_uploads_every_file()
    {
        var summary = await Migration().RunAsync(Customer, Files).WithTimeout();

        Assert.Equal(10, summary.FilesUploaded);
        Assert.Equal(10, _platform.FilesIn(summary.MigrationId).Count);
    }

    [Fact]
    public async Task Crash_during_upload_then_resume_uploads_each_file_exactly_once()
    {
        _platform.CrashAfterUpload = 4;
        await Assert.ThrowsAsync<SimulatedCrashException>(() => Migration().RunAsync(Customer, Files));
        _platform.CrashAfterUpload = null;

        var summary = await Migration().RunAsync(Customer, Files).WithTimeout();

        var stored = _platform.FilesIn(summary.MigrationId);
        Assert.Equal(10, stored.Distinct().Count());
        Assert.Equal(10, stored.Count);
    }

    [Fact]
    public async Task Resume_continues_the_interrupted_migration()
    {
        _platform.CrashAfterUpload = 4;
        await Assert.ThrowsAsync<SimulatedCrashException>(() => Migration().RunAsync(Customer, Files));
        _platform.CrashAfterUpload = null;

        var summary = await Migration().RunAsync(Customer, Files).WithTimeout();

        Assert.Equal(1, _platform.CreateCalls);
        Assert.Equal(_platform.CreatedMigrationIds[0], summary.MigrationId);
    }

    [Fact]
    public async Task Resume_uploads_only_what_is_missing()
    {
        _platform.CrashAfterUpload = 4;
        await Assert.ThrowsAsync<SimulatedCrashException>(() => Migration().RunAsync(Customer, Files));
        _platform.CrashAfterUpload = null;
        _platform.ResetUploadCounter();

        var summary = await Migration().RunAsync(Customer, Files).WithTimeout();

        Assert.Equal(6, summary.FilesUploaded);
        Assert.Equal(4, summary.FilesSkipped);
        Assert.Equal(6, _platform.UploadCallsSinceReset);
    }

    [Fact]
    public async Task A_file_whose_upload_never_reached_the_platform_is_uploaded_on_resume()
    {
        _platform.CrashBeforeUpload = 3; // the app dies after recording progress but before the platform sees the file
        await Assert.ThrowsAsync<SimulatedCrashException>(() => Migration().RunAsync(Customer, Files));
        _platform.CrashBeforeUpload = null;

        var summary = await Migration().RunAsync(Customer, Files).WithTimeout();

        Assert.Equal(Files.Select(f => f.FileId).Order(), _platform.FilesIn(summary.MigrationId).Order());
    }

    [Fact]
    public async Task A_checkpoint_damaged_by_the_crash_does_not_start_a_second_migration()
    {
        _platform.CrashAfterUpload = 4;
        await Assert.ThrowsAsync<SimulatedCrashException>(() => Migration().RunAsync(Customer, Files));
        _platform.CrashAfterUpload = null;
        TruncateCheckpoint();

        var summary = await Migration().RunAsync(Customer, Files).WithTimeout();

        Assert.Equal(1, _platform.CreateCalls);
        var stored = _platform.FilesIn(summary.MigrationId);
        Assert.Equal(10, stored.Count);
        Assert.Equal(10, stored.Distinct().Count());
    }

    [Fact]
    public async Task A_missing_checkpoint_does_not_re_upload_what_the_platform_already_has()
    {
        _platform.CrashAfterUpload = 4;
        await Assert.ThrowsAsync<SimulatedCrashException>(() => Migration().RunAsync(Customer, Files));
        _platform.CrashAfterUpload = null;
        File.Delete(_checkpoints.PathFor(Customer)); // the user "fixed" it by deleting the file

        var summary = await Migration().RunAsync(Customer, Files).WithTimeout();

        Assert.Equal(6, summary.FilesUploaded);
        Assert.Equal(10, _platform.FilesIn(summary.MigrationId).Count);
    }

    public void Dispose() => _appData.Dispose();

    private ResumableMigration Migration() => new(_platform, _checkpoints);

    /// <summary>Leaves the checkpoint as a crash during File.WriteAllText would: cut in half.</summary>
    private void TruncateCheckpoint()
    {
        var path = _checkpoints.PathFor(Customer);
        var json = File.ReadAllText(path);
        File.WriteAllText(path, json[..(json.Length / 2)]);
    }

    private sealed class FakePlatform : IMigrationPlatform
    {
        private readonly ConcurrentDictionary<string, List<string>> _byMigration = new();
        private int _uploadNumber;

        public List<string> CreatedMigrationIds { get; } = new();

        public int CreateCalls => CreatedMigrationIds.Count;

        public int UploadCallsSinceReset { get; private set; }

        /// <summary>The platform stores the file, then the app dies (an ambiguous outcome for the client).</summary>
        public int? CrashAfterUpload { get; set; }

        /// <summary>The app dies before the platform ever sees the file.</summary>
        public int? CrashBeforeUpload { get; set; }

        public IReadOnlyList<string> FilesIn(string migrationId) =>
            _byMigration.TryGetValue(migrationId, out var files) ? files.ToArray() : [];

        public void ResetUploadCounter() => UploadCallsSinceReset = 0;

        public Task<string> CreateMigrationAsync(string customerId, CancellationToken cancellationToken)
        {
            var id = $"mig-{CreatedMigrationIds.Count + 1:D5}";
            CreatedMigrationIds.Add(id);
            _byMigration[id] = new List<string>();
            return Task.FromResult(id);
        }

        public Task<string?> FindOpenMigrationAsync(string customerId, CancellationToken cancellationToken) =>
            Task.FromResult(CreatedMigrationIds.Count > 0 ? CreatedMigrationIds[^1] : null);

        public async Task<string> UploadFileAsync(string migrationId, SourceFile file, CancellationToken cancellationToken)
        {
            await Task.Yield();
            _uploadNumber++;
            UploadCallsSinceReset++;

            if (_uploadNumber == CrashBeforeUpload)
            {
                throw new SimulatedCrashException($"the process was killed before upload #{_uploadNumber}");
            }

            var files = _byMigration.GetOrAdd(migrationId, _ => new List<string>());
            files.Add(file.FileId);

            if (_uploadNumber == CrashAfterUpload)
            {
                throw new SimulatedCrashException($"the process was killed after upload #{_uploadNumber}");
            }

            return $"file-{files.Count:D5}";
        }

        public Task<IReadOnlyList<string>> ListUploadedFileIdsAsync(string migrationId, CancellationToken cancellationToken) =>
            Task.FromResult(FilesIn(migrationId));
    }
}
