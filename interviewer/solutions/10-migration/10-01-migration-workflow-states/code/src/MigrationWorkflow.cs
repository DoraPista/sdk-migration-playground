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
        // A workflow instance represents ONE migration. Re-running it would create a second migration on
        // the platform and duplicate everything (see 04-02).
        if (State != MigrationState.NotStarted)
        {
            throw new InvalidOperationException($"This migration has already run (state: {State}). Create a new workflow to migrate again.");
        }

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

            // --- Validate: blocking issues end the migration BEFORE anything exists on the platform.
            State = MigrationState.Validating;
            _stages.Add("Validate");
            var validation = await _validator.ValidateAsync(files, cancellationToken);
            foreach (var warning in validation.Warnings)
            {
                _logger.LogWarning("Validation warning: {Warning}", warning);
            }

            if (validation.BlockingIssues.Count > 0)
            {
                _logger.LogError("Validation found {Count} blocking issue(s)", validation.BlockingIssues.Count);
                return Finish(MigrationState.Failed, uploaded, failed, string.Join("; ", validation.BlockingIssues), migrationId);
            }

            _stages.Add("CreateMigration");
            migrationId = await _platform.CreateMigrationAsync(customerId, destinationId, cancellationToken);

            // --- Upload: one bad file doesn't stop the others, but it does stop completion.
            State = MigrationState.Uploading;
            _stages.Add("UploadFiles");
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    await _platform.UploadFileAsync(migrationId, file, cancellationToken);
                    uploaded++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Upload of {File} failed", file);
                    failed.Add(file);
                }
            }

            if (failed.Count > 0)
            {
                // Completing now would seal an incomplete archive: the platform accepts no more files afterwards.
                return Finish(MigrationState.Failed, uploaded, failed, $"{failed.Count} file(s) could not be uploaded; the migration was left open so it can be resumed.", migrationId);
            }

            // --- Verify: the platform's own check of what it received.
            State = MigrationState.Verifying;
            _stages.Add("Verify");
            if (!await _platform.VerifyAsync(migrationId, cancellationToken))
            {
                return Finish(MigrationState.Failed, uploaded, failed, "The platform could not verify the uploaded archive.", migrationId);
            }

            // --- Complete: only the platform's confirmation makes a migration complete.
            _stages.Add("Complete");
            await _platform.CompleteAsync(migrationId, cancellationToken);
            return Finish(MigrationState.Completed, uploaded, failed, null, migrationId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Migration cancelled by the user");
            return Finish(MigrationState.Cancelled, uploaded, failed, null, migrationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Migration failed");
            return Finish(MigrationState.Failed, uploaded, failed, ex.Message, migrationId);
        }
    }

    private WorkflowResult Finish(MigrationState state, int uploaded, IReadOnlyList<string> failed, string? error, string? migrationId)
    {
        State = state;
        return new WorkflowResult(state, uploaded, failed, error)
        {
            StagesRun = _stages.ToArray(),
            MigrationId = migrationId,
        };
    }
}
