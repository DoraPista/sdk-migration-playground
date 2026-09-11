namespace MigrationKit.Pipeline;

/// <summary>State that flows through the stages.</summary>
public sealed class MigrationContext(MigrationJob job, MigrationOptions options)
{
    public MigrationJob Job { get; } = job;

    public MigrationOptions Options { get; } = options;

    public string? DestinationId { get; set; }

    public string? MigrationId { get; set; }

    public int FilesUploaded { get; set; }

    public int ThumbnailsGenerated { get; set; }

    public List<ValidationIssue> Issues { get; } = new();

    public WorkflowReport ToReport(bool succeeded, IReadOnlyList<string> stagesRun, string? error) =>
        new(succeeded, stagesRun, Issues, FilesUploaded, ThumbnailsGenerated, error);
}

public readonly record struct StageOutcome(bool Succeeded, string? Error)
{
    public static StageOutcome Ok { get; } = new(true, null);

    public static StageOutcome Fail(string error) => new(false, error);
}

public interface IMigrationStage
{
    string Name { get; }

    bool ShouldRun(MigrationContext context) => true;

    Task<StageOutcome> ExecuteAsync(MigrationContext context, CancellationToken cancellationToken);
}

internal sealed class AuthenticateStage(IMigrationPlatform platform) : IMigrationStage
{
    public string Name => "Authenticate";

    public async Task<StageOutcome> ExecuteAsync(MigrationContext context, CancellationToken cancellationToken)
    {
        await platform.AuthenticateAsync(cancellationToken);
        return StageOutcome.Ok;
    }
}

internal sealed class ProvisionStage(IMigrationPlatform platform) : IMigrationStage
{
    public string Name => "Provision";

    public async Task<StageOutcome> ExecuteAsync(MigrationContext context, CancellationToken cancellationToken)
    {
        context.DestinationId = await platform.ProvisionAsync(context.Job.CustomerId, cancellationToken);
        return StageOutcome.Ok;
    }
}

/// <summary>Pre-flight checks. Runs before anything exists on the platform.</summary>
internal sealed class ValidateStage : IMigrationStage
{
    public string Name => "Validate";

    public Task<StageOutcome> ExecuteAsync(MigrationContext context, CancellationToken cancellationToken)
    {
        context.Issues.AddRange(SourceValidator.Validate(context.Job));
        return Task.FromResult(context.Issues.Count == 0
            ? StageOutcome.Ok
            : StageOutcome.Fail($"The export has {context.Issues.Count} problem(s): {string.Join("; ", context.Issues.Take(3).Select(i => i.Code))}…"));
    }
}

internal sealed class CreateMigrationStage(IMigrationPlatform platform) : IMigrationStage
{
    public string Name => "CreateMigration";

    public async Task<StageOutcome> ExecuteAsync(MigrationContext context, CancellationToken cancellationToken)
    {
        context.MigrationId = await platform.CreateMigrationAsync(context.Job.CustomerId, context.DestinationId!, cancellationToken);
        return StageOutcome.Ok;
    }
}

internal sealed class UploadMetadataStage(IMigrationPlatform platform) : IMigrationStage
{
    public string Name => "UploadMetadata";

    public bool ShouldRun(MigrationContext context) => context.Options.UploadMetadata;

    public async Task<StageOutcome> ExecuteAsync(MigrationContext context, CancellationToken cancellationToken)
    {
        if (!context.Options.DryRun)
        {
            await platform.UploadMetadataAsync(context.MigrationId!, context.Job, cancellationToken);
        }

        return StageOutcome.Ok;
    }
}

internal sealed class UploadFilesStage(IMigrationPlatform platform) : IMigrationStage
{
    public string Name => "UploadFiles";

    public async Task<StageOutcome> ExecuteAsync(MigrationContext context, CancellationToken cancellationToken)
    {
        foreach (var file in context.Job.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (context.Options.DryRun)
            {
                continue;
            }

            await platform.UploadFileAsync(context.MigrationId!, file, cancellationToken);
            context.FilesUploaded++;
        }

        return StageOutcome.Ok;
    }
}

internal sealed class ThumbnailStage(IMigrationPlatform platform, IThumbnailGenerator? thumbnails) : IMigrationStage
{
    public string Name => "Thumbnails";

    public bool ShouldRun(MigrationContext context) => context.Options.GenerateThumbnails && thumbnails is not null;

    public async Task<StageOutcome> ExecuteAsync(MigrationContext context, CancellationToken cancellationToken)
    {
        if (context.Options.DryRun)
        {
            return StageOutcome.Ok;
        }

        foreach (var image in context.Job.Files.Where(f => string.Equals(f.Kind, "Image", StringComparison.OrdinalIgnoreCase)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var thumbnail = await thumbnails!.GenerateAsync(image, cancellationToken);
            await platform.UploadThumbnailAsync(context.MigrationId!, image, thumbnail, cancellationToken);
            context.ThumbnailsGenerated++;
        }

        return StageOutcome.Ok;
    }
}

internal sealed class VerifyStage(IMigrationPlatform platform) : IMigrationStage
{
    public string Name => "Verify";

    public bool ShouldRun(MigrationContext context) => !context.Options.SkipVerification;

    public async Task<StageOutcome> ExecuteAsync(MigrationContext context, CancellationToken cancellationToken)
    {
        if (context.Options.DryRun)
        {
            return StageOutcome.Ok;
        }

        return await platform.VerifyAsync(context.MigrationId!, cancellationToken)
            ? StageOutcome.Ok
            : StageOutcome.Fail("The platform could not verify the uploaded archive.");
    }
}

internal sealed class CompleteStage(IMigrationPlatform platform) : IMigrationStage
{
    public string Name => "Complete";

    public async Task<StageOutcome> ExecuteAsync(MigrationContext context, CancellationToken cancellationToken)
    {
        if (!context.Options.DryRun)
        {
            await platform.CompleteAsync(context.MigrationId!, cancellationToken);
        }

        return StageOutcome.Ok;
    }
}
