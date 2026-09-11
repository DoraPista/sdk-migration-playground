using System.Net;
using Gym.TestUtilities;
using Microsoft.Extensions.Logging.Abstractions;
using MigrationKit.Migration;

namespace Ex0202.Migration.Tests;

public sealed class MigrationServiceTests : IDisposable
{
    private readonly TestFiles _files = new();
    private readonly FakeApi _api = new();

    [Fact]
    public async Task Healthy_source_is_migrated()
    {
        CreateSource(3);

        var result = await Service().RunAsync(_files.Root);

        Assert.Equal(MigrationStatus.Succeeded, result.Status);
        Assert.Equal(3, result.FilesUploaded);
        Assert.True(_api.Completed);
    }

    [Fact]
    public async Task Unreachable_source_folder_fails_the_migration()
    {
        var missing = Path.Combine(_files.Root, "disconnected-share", "Projects");

        var result = await Service().RunAsync(missing);

        Assert.Equal(MigrationStatus.Failed, result.Status);
        Assert.Contains("disconnected-share", result.Error);
        Assert.False(_api.Completed);
        Assert.Equal(0, _api.ManifestUploads);
    }

    [Fact]
    public async Task Rejected_manifest_fails_the_migration_before_any_file_is_uploaded()
    {
        CreateSource(3);
        _api.ManifestFailure = new HttpRequestException("Manifest rejected", null, HttpStatusCode.InternalServerError);

        var result = await Service().RunAsync(_files.Root);

        Assert.Equal(MigrationStatus.Failed, result.Status);
        Assert.Empty(_api.UploadedFiles);
        Assert.False(_api.Completed);
    }

    [Fact]
    public async Task Unreadable_file_is_reported_and_the_other_files_are_uploaded()
    {
        CreateSource(3);
        var lockedPath = _files.Create(Path.Combine("source", "locked.dwg"), 100);
        using var otherProgram = new FileStream(lockedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var result = await Service().RunAsync(Path.Combine(_files.Root, "source"));

        Assert.Equal(MigrationStatus.PartiallySucceeded, result.Status);
        Assert.Equal(3, result.FilesUploaded);
        var failure = Assert.Single(result.FailedFiles);
        Assert.Equal("locked.dwg", failure.RelativePath);
        Assert.False(_api.Completed);
    }

    [Fact]
    public async Task Cancellation_is_reported_as_cancellation()
    {
        CreateSource(5);
        using var cts = new CancellationTokenSource();
        _api.OnUpload = count =>
        {
            if (count == 2)
            {
                cts.Cancel();
            }
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service().RunAsync(Path.Combine(_files.Root, "source"), cts.Token));
        Assert.False(_api.Completed);
    }

    public void Dispose() => _files.Dispose();

    private MigrationService Service() => new(_api, new SourceScanner(), NullLogger<MigrationService>.Instance);

    private void CreateSource(int count)
    {
        for (var i = 1; i <= count; i++)
        {
            _files.Create(Path.Combine("source", $"doc-{i}.pdf"), 1_000, seed: i);
        }
    }

    private sealed class FakeApi : IMigrationApi
    {
        public Exception? ManifestFailure { get; set; }
        public Action<int>? OnUpload { get; set; }
        public int ManifestUploads { get; private set; }
        public List<string> UploadedFiles { get; } = new();
        public bool Completed { get; private set; }

        public Task UploadManifestAsync(IReadOnlyList<string> relativePaths, CancellationToken cancellationToken)
        {
            ManifestUploads++;
            return ManifestFailure is null ? Task.CompletedTask : Task.FromException(ManifestFailure);
        }

        public async Task UploadFileAsync(string relativePath, Stream content, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await content.CopyToAsync(Stream.Null, cancellationToken);
            UploadedFiles.Add(relativePath);
            OnUpload?.Invoke(UploadedFiles.Count);
        }

        public Task CompleteAsync(CancellationToken cancellationToken)
        {
            Completed = true;
            return Task.CompletedTask;
        }
    }
}
