using System.Text.Json;
using Gym.TestUtilities;
using MigrationKit.Pipeline;

namespace Ex1002.Pipeline.Tests;

public sealed class NewStageTests
{
    private readonly FakePlatform _platform = new();
    private readonly FakeThumbnails _thumbnails = new();

    [Fact]
    public async Task Validation_reports_every_problem_in_the_export()
    {
        var report = await Run(TestJobs.Problematic, new MigrationOptions()).WithTimeout();

        Assert.False(report.Succeeded);
        Assert.Equal(
            [
                ValidationCodes.DuplicateFileId,
                ValidationCodes.EmptyPath,
                ValidationCodes.InvalidHash,
                ValidationCodes.NegativeSize,
                ValidationCodes.UnknownProject,
                ValidationCodes.UnsafePath,
            ],
            report.Issues.Select(i => i.Code).Distinct().Order());
    }

    [Fact]
    public async Task Blocking_issues_stop_the_migration_before_it_is_created()
    {
        var report = await Run(TestJobs.Problematic, new MigrationOptions()).WithTimeout();

        Assert.Equal(0, _platform.CreateCalls);
        Assert.Equal(0, _platform.FileUploads);
        Assert.Equal(["Authenticate", "Provision", "Validate"], report.StagesRun);
    }

    [Fact]
    public async Task A_clean_export_produces_no_issues()
    {
        var report = await Run(TestJobs.Healthy, new MigrationOptions()).WithTimeout();

        Assert.Empty(report.Issues);
        Assert.True(report.Succeeded);
    }

    [Fact]
    public async Task Northwinds_broken_export_is_rejected()
    {
        var job = TestJobs.FromMalformedMockData();

        var report = await Run(job, new MigrationOptions()).WithTimeout();

        Assert.False(report.Succeeded);
        Assert.Contains(report.Issues, i => i.Code == ValidationCodes.DuplicateFileId && i.Subject == "F-9001");
        Assert.Contains(report.Issues, i => i.Code == ValidationCodes.UnsafePath);
        Assert.Contains(report.Issues, i => i.Code == ValidationCodes.NegativeSize && i.Subject == "F-9003");
        Assert.Contains(report.Issues, i => i.Code == ValidationCodes.InvalidHash && i.Subject == "F-9004");
    }

    [Fact]
    public async Task Thumbnails_are_generated_for_images_when_enabled()
    {
        var report = await Run(TestJobs.Healthy, new MigrationOptions { GenerateThumbnails = true }).WithTimeout();

        Assert.Contains("Thumbnails", report.StagesRun);
        Assert.Equal(1, report.ThumbnailsGenerated);
        Assert.Equal(["F-0003"], _thumbnails.Generated);
        Assert.Equal(1, _platform.ThumbnailUploads);
    }

    [Fact]
    public async Task Thumbnails_are_not_generated_when_disabled()
    {
        var report = await Run(TestJobs.Healthy, new MigrationOptions { GenerateThumbnails = false }).WithTimeout();

        Assert.DoesNotContain("Thumbnails", report.StagesRun);
        Assert.Equal(0, report.ThumbnailsGenerated);
        Assert.Empty(_thumbnails.Generated);
    }

    [Fact]
    public async Task Thumbnails_are_skipped_in_a_dry_run()
    {
        var report = await Run(TestJobs.Healthy, new MigrationOptions { GenerateThumbnails = true, DryRun = true }).WithTimeout();

        Assert.True(report.Succeeded);
        Assert.Equal(0, _platform.ThumbnailUploads);
    }

    private Task<WorkflowReport> Run(MigrationJob job, MigrationOptions options) =>
        new MigrationWorkflow(_platform, options, _thumbnails).RunAsync(job);
}

internal static class TestJobs
{
    public static MigrationJob Healthy { get; } = new(
        "CUST-1001",
        ["PRJ-2001", "PRJ-2002"],
        [
            new SourceFile("F-0001", "PRJ-2001", "documents/project-brief.txt", 96, new string('a', 64), "Document"),
            new SourceFile("F-0002", "PRJ-2001", "documents/specification.pdf", 48_000, new string('b', 64), "Document"),
            new SourceFile("F-0003", "PRJ-2002", "images/deck-inspection-001.jpg", 182_000, new string('c', 64), "Image"),
        ]);

    public static MigrationJob Problematic { get; } = new(
        "CUST-1002",
        ["PRJ-9001"],
        [
            new SourceFile("F-1", "PRJ-9001", "documents/a.pdf", 10, new string('a', 64), "Document"),
            new SourceFile("F-1", "PRJ-9001", "documents/b.pdf", 10, new string('a', 64), "Document"),
            new SourceFile("F-2", "PRJ-0000", "documents/c.pdf", 10, new string('a', 64), "Document"),
            new SourceFile("F-3", "PRJ-9001", "documents/d.pdf", -1, new string('a', 64), "Document"),
            new SourceFile("F-4", "PRJ-9001", "documents/e.pdf", 10, "not-a-hash", "Document"),
            new SourceFile("F-5", "PRJ-9001", "../../Windows/System32/drivers/etc/hosts", 824, null, "Document"),
            new SourceFile("F-6", "PRJ-9001", "  ", 10, null, "Document"),
        ]);

    /// <summary>The malformed export in shared/MockData.</summary>
    public static MigrationJob FromMalformedMockData()
    {
        var files = MockDataPaths.Files("malformed")
            .Select(f => new SourceFile(f.Id, f.ProjectId ?? string.Empty, f.RelativePath ?? string.Empty, f.SizeBytes, f.Sha256, f.Kind ?? "Document"))
            .ToList();
        var projects = MockDataPaths.Projects("malformed").Select(p => p.Id).ToList();
        return new MigrationJob("CUST-1002", projects, files);
    }
}

internal sealed class FakeThumbnails : IThumbnailGenerator
{
    private readonly List<string> _generated = new();

    public IReadOnlyList<string> Generated => _generated;

    public Task<byte[]> GenerateAsync(SourceFile file, CancellationToken cancellationToken)
    {
        _generated.Add(file.Id);
        return Task.FromResult<byte[]>([0x89, (byte)'P', (byte)'N', (byte)'G']);
    }
}

internal sealed class FakePlatform : IMigrationPlatform
{
    public bool VerificationResult { get; set; } = true;
    public Action<SourceFile>? OnFileUpload { get; set; }
    public int CreateCalls { get; private set; }
    public int MetadataUploads { get; private set; }
    public int FileUploads { get; private set; }
    public int ThumbnailUploads { get; private set; }
    public int CompleteCalls { get; private set; }

    public Task AuthenticateAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<string> ProvisionAsync(string customerId, CancellationToken cancellationToken) => Task.FromResult("dst-00011");

    public Task<string> CreateMigrationAsync(string customerId, string destinationId, CancellationToken cancellationToken)
    {
        CreateCalls++;
        return Task.FromResult("mig-00102");
    }

    public Task UploadMetadataAsync(string migrationId, MigrationJob job, CancellationToken cancellationToken)
    {
        MetadataUploads++;
        return Task.CompletedTask;
    }

    public async Task UploadFileAsync(string migrationId, SourceFile file, CancellationToken cancellationToken)
    {
        await Task.Yield();
        OnFileUpload?.Invoke(file);
        cancellationToken.ThrowIfCancellationRequested();
        FileUploads++;
    }

    public Task UploadThumbnailAsync(string migrationId, SourceFile file, byte[] thumbnail, CancellationToken cancellationToken)
    {
        ThumbnailUploads++;
        return Task.CompletedTask;
    }

    public Task<bool> VerifyAsync(string migrationId, CancellationToken cancellationToken) => Task.FromResult(VerificationResult);

    public Task CompleteAsync(string migrationId, CancellationToken cancellationToken)
    {
        CompleteCalls++;
        return Task.CompletedTask;
    }
}
