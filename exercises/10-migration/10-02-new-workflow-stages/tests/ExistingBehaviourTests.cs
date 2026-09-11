using Gym.TestUtilities;
using MigrationKit.Pipeline;

namespace Ex1002.Pipeline.Tests;

/// <summary>What the workflow already does. These must keep passing after the restructuring.</summary>
public sealed class ExistingBehaviourTests
{
    private readonly FakePlatform _platform = new();

    [Fact]
    public async Task Default_run_executes_the_stages_in_order()
    {
        var report = await Run(new MigrationOptions()).WithTimeout();

        Assert.True(report.Succeeded, report.Error);
        Assert.Equal(["Authenticate", "Provision", "Validate", "CreateMigration", "UploadMetadata", "UploadFiles", "Verify", "Complete"], report.StagesRun);
        Assert.Equal(3, report.FilesUploaded);
    }

    [Fact]
    public async Task Metadata_upload_can_be_switched_off()
    {
        var report = await Run(new MigrationOptions { UploadMetadata = false }).WithTimeout();

        Assert.DoesNotContain("UploadMetadata", report.StagesRun);
        Assert.Equal(0, _platform.MetadataUploads);
    }

    [Fact]
    public async Task Verification_can_be_skipped()
    {
        var report = await Run(new MigrationOptions { SkipVerification = true }).WithTimeout();

        Assert.DoesNotContain("Verify", report.StagesRun);
        Assert.True(report.Succeeded);
    }

    [Fact]
    public async Task Dry_run_changes_nothing_on_the_platform()
    {
        var report = await Run(new MigrationOptions { DryRun = true }).WithTimeout();

        Assert.True(report.Succeeded);
        Assert.Equal(0, report.FilesUploaded);
        Assert.Equal(0, _platform.FileUploads);
        Assert.Equal(0, _platform.CompleteCalls);
    }

    [Fact]
    public async Task Failed_verification_fails_the_migration()
    {
        _platform.VerificationResult = false;

        var report = await Run(new MigrationOptions()).WithTimeout();

        Assert.False(report.Succeeded);
        Assert.Equal(0, _platform.CompleteCalls);
    }

    [Fact]
    public async Task Cancellation_is_reported()
    {
        using var cts = new CancellationTokenSource();
        _platform.OnFileUpload = _ => cts.Cancel();

        var report = await Run(new MigrationOptions(), cts.Token).WithTimeout();

        Assert.False(report.Succeeded);
        Assert.Contains("cancel", report.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, _platform.CompleteCalls);
    }

    private Task<WorkflowReport> Run(MigrationOptions options, CancellationToken cancellationToken = default) =>
        new MigrationWorkflow(_platform, options, new FakeThumbnails()).RunAsync(TestJobs.Healthy, cancellationToken);
}
