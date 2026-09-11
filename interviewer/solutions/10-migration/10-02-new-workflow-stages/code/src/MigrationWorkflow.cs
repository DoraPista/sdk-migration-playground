namespace MigrationKit.Pipeline;

/// <summary>
/// Runs the migration as an ordered list of stages. Adding a stage means adding a class and one line
/// to <see cref="BuildStages"/>; no existing stage changes, and nothing grows a new `if`.
/// </summary>
public sealed class MigrationWorkflow
{
    private readonly IReadOnlyList<IMigrationStage> _stages;
    private readonly MigrationOptions _options;

    public MigrationWorkflow(IMigrationPlatform platform, MigrationOptions options, IThumbnailGenerator? thumbnails = null)
    {
        _options = options;
        _stages = BuildStages(platform, thumbnails);
    }

    public async Task<WorkflowReport> RunAsync(MigrationJob job, CancellationToken cancellationToken = default)
    {
        var context = new MigrationContext(job, _options);
        var stagesRun = new List<string>();

        try
        {
            foreach (var stage in _stages)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!stage.ShouldRun(context))
                {
                    continue;
                }

                stagesRun.Add(stage.Name);
                var outcome = await stage.ExecuteAsync(context, cancellationToken);
                if (!outcome.Succeeded)
                {
                    return context.ToReport(false, stagesRun, outcome.Error);
                }
            }

            return context.ToReport(true, stagesRun, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return context.ToReport(false, stagesRun, "The migration was cancelled.");
        }
        catch (Exception ex)
        {
            return context.ToReport(false, stagesRun, ex.Message);
        }
    }

    /// <summary>The migration, in order. This list IS the workflow.</summary>
    private static IReadOnlyList<IMigrationStage> BuildStages(IMigrationPlatform platform, IThumbnailGenerator? thumbnails) =>
    [
        new AuthenticateStage(platform),
        new ProvisionStage(platform),
        new ValidateStage(),
        new CreateMigrationStage(platform),
        new UploadMetadataStage(platform),
        new UploadFilesStage(platform),
        new ThumbnailStage(platform, thumbnails),
        new VerifyStage(platform),
        new CompleteStage(platform),
    ];
}
