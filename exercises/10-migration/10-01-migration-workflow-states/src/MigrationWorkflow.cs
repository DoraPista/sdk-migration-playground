using Microsoft.Extensions.Logging;

namespace MigrationKit.Workflow;

public sealed class MigrationWorkflow
{
    private readonly IMigrationPlatform _platform;
    private readonly ISourceValidator _validator;
    private readonly ILogger<MigrationWorkflow> _logger;
    private readonly List<string> _stages = new();

    public MigrationWorkflow(IMigrationPlatform platform, ISourceValidator validator, ILogger<MigrationWorkflow> logger)
    {
        _platform = platform;
        _validator = validator;
        _logger = logger;
    }

    public MigrationState State { get; private set; } = MigrationState.NotStarted;

    public async Task<WorkflowResult> RunAsync(string customerId, IReadOnlyList<string> files, CancellationToken cancellationToken = default)
    {
        var failed = new List<string>();
        var uploaded = 0;
        string? migrationId = null;

        try
        {
            State = MigrationState.Authenticating;
            _stages.Add("Authenticate");
            await _platform.AuthenticateAsync(cancellationToken);

            State = MigrationState.Provisioning;
            _stages.Add("Provision");
            var destinationId = await _platform.ProvisionAsync(customerId, cancellationToken);

            State = MigrationState.Validating;
            _stages.Add("Validate");
            var validation = await _validator.ValidateAsync(files, cancellationToken);
            if (validation.BlockingIssues.Count > 0)
            {
                _logger.LogWarning("Validation found {Count} issue(s): {Issues}", validation.BlockingIssues.Count, string.Join("; ", validation.BlockingIssues));
            }

            _stages.Add("CreateMigration");
            migrationId = await _platform.CreateMigrationAsync(customerId, destinationId, cancellationToken);

            State = MigrationState.Uploading;
            _stages.Add("UploadFiles");
            foreach (var file in files)
            {
                try
                {
                    await _platform.UploadFileAsync(migrationId, file, cancellationToken);
                    uploaded++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Upload of {File} failed", file);
                    failed.Add(file);
                }
            }

            State = MigrationState.Verifying;
            _stages.Add("Verify");
            var verified = await _platform.VerifyAsync(migrationId, cancellationToken);

            _stages.Add("Complete");
            try
            {
                await _platform.CompleteAsync(migrationId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not mark the migration complete");
            }

            State = MigrationState.Completed;
            return new WorkflowResult(State, uploaded, failed, null) { StagesRun = _stages.ToArray(), MigrationId = migrationId };
        }
        catch (Exception ex)
        {
            State = MigrationState.Failed;
            _logger.LogError(ex, "Migration failed");
            return new WorkflowResult(State, uploaded, failed, ex.Message) { StagesRun = _stages.ToArray(), MigrationId = migrationId };
        }
    }
}
