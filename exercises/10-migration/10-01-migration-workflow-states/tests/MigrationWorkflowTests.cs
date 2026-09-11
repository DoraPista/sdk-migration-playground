using Gym.TestUtilities;
using Microsoft.Extensions.Logging.Abstractions;
using MigrationKit.Workflow;

namespace Ex1001.Workflow.Tests;

public sealed class MigrationWorkflowTests
{
    private static readonly string[] Files = [@"C:\Projects\a.pdf", @"C:\Projects\b.dwg", @"C:\Projects\c.jpg"];

    private readonly FakePlatform _platform = new();
    private readonly FakeValidator _validator = new();

    [Fact]
    public async Task Healthy_migration_runs_every_stage_and_completes()
    {
        var result = await Workflow().RunAsync("CUST-1001", Files).WithTimeout();

        Assert.Equal(MigrationState.Completed, result.State);
        Assert.Equal(3, result.FilesUploaded);
        Assert.Equal(["Authenticate", "Provision", "Validate", "CreateMigration", "UploadFiles", "Verify", "Complete"], result.StagesRun);
        Assert.Equal(1, _platform.CompleteCalls);
    }

    [Fact]
    public async Task Blocking_validation_issues_stop_the_migration_before_it_is_created()
    {
        _validator.Result = new ValidationResult(["40 files in the manifest do not exist"], []);

        var result = await Workflow().RunAsync("CUST-1001", Files).WithTimeout();

        Assert.Equal(MigrationState.Failed, result.State);
        Assert.Contains("do not exist", result.Error);
        Assert.Equal(0, _platform.CreateCalls);
        Assert.Equal(0, _platform.CompleteCalls);
    }

    [Fact]
    public async Task Warnings_do_not_stop_the_migration()
    {
        _validator.Result = new ValidationResult([], ["2 files are empty"]);

        var result = await Workflow().RunAsync("CUST-1001", Files).WithTimeout();

        Assert.Equal(MigrationState.Completed, result.State);
    }

    [Fact]
    public async Task A_failed_file_prevents_completion()
    {
        _platform.FailUploadOf = @"C:\Projects\b.dwg";

        var result = await Workflow().RunAsync("CUST-1001", Files).WithTimeout();

        Assert.Equal(MigrationState.Failed, result.State);
        Assert.Equal([@"C:\Projects\b.dwg"], result.FailedFiles);
        Assert.Equal(2, result.FilesUploaded); // the other files were still uploaded
        Assert.Equal(0, _platform.CompleteCalls);
    }

    [Fact]
    public async Task A_failed_verification_prevents_completion()
    {
        _platform.VerificationResult = false;

        var result = await Workflow().RunAsync("CUST-1001", Files).WithTimeout();

        Assert.Equal(MigrationState.Failed, result.State);
        Assert.Equal(0, _platform.CompleteCalls);
    }

    [Fact]
    public async Task A_completion_the_platform_rejected_is_not_reported_as_completed()
    {
        _platform.FailComplete = true;

        var result = await Workflow().RunAsync("CUST-1001", Files).WithTimeout();

        Assert.NotEqual(MigrationState.Completed, result.State);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task Cancellation_is_reported_as_cancellation()
    {
        using var cts = new CancellationTokenSource();
        _platform.OnUpload = _ => cts.Cancel();

        var result = await Workflow().RunAsync("CUST-1001", Files, cts.Token).WithTimeout();

        Assert.Equal(MigrationState.Cancelled, result.State);
        Assert.Equal(0, _platform.CompleteCalls);
    }

    [Fact]
    public async Task A_finished_workflow_cannot_be_run_again()
    {
        var workflow = Workflow();
        await workflow.RunAsync("CUST-1001", Files).WithTimeout();

        await Assert.ThrowsAsync<InvalidOperationException>(() => workflow.RunAsync("CUST-1001", Files));
        Assert.Equal(1, _platform.CreateCalls);
    }

    private MigrationWorkflow Workflow() => new(_platform, _validator, NullLogger<MigrationWorkflow>.Instance);

    private sealed class FakeValidator : ISourceValidator
    {
        public ValidationResult Result { get; set; } = ValidationResult.Clean;

        public Task<ValidationResult> ValidateAsync(IReadOnlyList<string> files, CancellationToken cancellationToken) => Task.FromResult(Result);
    }

    private sealed class FakePlatform : IMigrationPlatform
    {
        public string? FailUploadOf { get; set; }
        public bool FailComplete { get; set; }
        public bool VerificationResult { get; set; } = true;
        public Action<string>? OnUpload { get; set; }
        public int CreateCalls { get; private set; }
        public int CompleteCalls { get; private set; }

        public Task AuthenticateAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<string> ProvisionAsync(string customerId, CancellationToken cancellationToken) => Task.FromResult("dst-00011");

        public Task<string> CreateMigrationAsync(string customerId, string destinationId, CancellationToken cancellationToken)
        {
            CreateCalls++;
            return Task.FromResult("mig-00102");
        }

        public async Task UploadFileAsync(string migrationId, string path, CancellationToken cancellationToken)
        {
            await Task.Yield();
            OnUpload?.Invoke(path);
            cancellationToken.ThrowIfCancellationRequested();
            if (path == FailUploadOf)
            {
                throw new IOException($"{path} could not be read.");
            }
        }

        public Task<bool> VerifyAsync(string migrationId, CancellationToken cancellationToken) => Task.FromResult(VerificationResult);

        public Task CompleteAsync(string migrationId, CancellationToken cancellationToken)
        {
            CompleteCalls++;
            return FailComplete
                ? Task.FromException(new HttpRequestException("The platform rejected the completion request (503)."))
                : Task.CompletedTask;
        }
    }
}
