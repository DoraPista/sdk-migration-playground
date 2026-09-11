namespace MigrationKit.Pipeline;

public sealed class MigrationWorkflow
{
    private readonly IMigrationPlatform _platform;
    private readonly IThumbnailGenerator? _thumbnails;
    private readonly MigrationOptions _options;

    public MigrationWorkflow(IMigrationPlatform platform, MigrationOptions options, IThumbnailGenerator? thumbnails = null)
    {
        _platform = platform;
        _options = options;
        _thumbnails = thumbnails;
    }

    public async Task<WorkflowReport> RunAsync(MigrationJob job, CancellationToken cancellationToken = default)
    {
        var stages = new List<string>();
        var uploaded = 0;

        try
        {
            stages.Add("Authenticate");
            await _platform.AuthenticateAsync(cancellationToken);

            stages.Add("Provision");
            var destinationId = await _platform.ProvisionAsync(job.CustomerId, cancellationToken);

            stages.Add("CreateMigration");
            var migrationId = await _platform.CreateMigrationAsync(job.CustomerId, destinationId, cancellationToken);

            if (_options.UploadMetadata)
            {
                stages.Add("UploadMetadata");
                if (!_options.DryRun)
                {
                    await _platform.UploadMetadataAsync(migrationId, job, cancellationToken);
                }
            }

            stages.Add("UploadFiles");
            foreach (var file in job.Files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (_options.DryRun)
                {
                    continue;
                }

                await _platform.UploadFileAsync(migrationId, file, cancellationToken);
                uploaded++;
            }

            if (!_options.SkipVerification)
            {
                stages.Add("Verify");
                if (!_options.DryRun && !await _platform.VerifyAsync(migrationId, cancellationToken))
                {
                    return new WorkflowReport(false, stages, [], uploaded, 0, "The platform could not verify the uploaded archive.");
                }
            }

            stages.Add("Complete");
            if (!_options.DryRun)
            {
                await _platform.CompleteAsync(migrationId, cancellationToken);
            }

            return new WorkflowReport(true, stages, [], uploaded, 0, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new WorkflowReport(false, stages, [], uploaded, 0, "The migration was cancelled.");
        }
        catch (Exception ex)
        {
            return new WorkflowReport(false, stages, [], uploaded, 0, ex.Message);
        }
    }
}
