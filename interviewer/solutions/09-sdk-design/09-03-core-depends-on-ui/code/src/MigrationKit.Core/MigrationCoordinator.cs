namespace MigrationKit.Core;

/// <summary>Reports that one file changed state. Hosts turn this into whatever their UI needs.</summary>
public sealed record FileStatusChangedEventArgs(string Path, FileStatus Status, string? Error);

public enum FileStatus
{
    Pending,
    Uploaded,
    Failed,
}

/// <summary>
/// Runs a migration: uploads the files, tracks their state and reports progress.
/// Knows nothing about UI: no dispatcher, no dialogs, no image types. Events are raised on the thread
/// that runs the migration; hosts marshal to their own UI thread.
/// </summary>
public sealed class MigrationCoordinator
{
    private static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png"];

    private readonly IFileUploader _uploader;
    private readonly IThumbnailRenderer? _thumbnailRenderer;

    public MigrationCoordinator(IFileUploader uploader, IThumbnailRenderer? thumbnailRenderer = null)
    {
        _uploader = uploader;
        _thumbnailRenderer = thumbnailRenderer;
    }

    public event EventHandler<MigrationProgress>? ProgressChanged;

    /// <summary>Raised as each file starts, succeeds or fails. The host owns its own list for binding.</summary>
    public event EventHandler<FileStatusChangedEventArgs>? FileStatusChanged;

    public async Task<MigrationOutcome> RunAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken = default)
    {
        var outcomes = new List<FileOutcome>(paths.Count);

        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Raise(FileStatusChanged, new FileStatusChangedEventArgs(path, FileStatus.Pending, null));

            try
            {
                await _uploader.UploadAsync(path, cancellationToken).ConfigureAwait(false);
                var thumbnail = IsImage(path) ? TryRenderThumbnail(path) : null;

                outcomes.Add(new FileOutcome(path, true, null, thumbnail));
                Raise(FileStatusChanged, new FileStatusChangedEventArgs(path, FileStatus.Uploaded, null));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The core reports problems; it never decides how to show them.
                outcomes.Add(new FileOutcome(path, false, ex.Message, null));
                Raise(FileStatusChanged, new FileStatusChangedEventArgs(path, FileStatus.Failed, ex.Message));
            }

            Raise(ProgressChanged, new MigrationProgress(outcomes.Count(o => o.Succeeded), paths.Count, path));
        }

        return new MigrationOutcome(outcomes.All(o => o.Succeeded), outcomes);
    }

    /// <summary>A thumbnail is a nicety: a host that can't render one must not fail the file.</summary>
    private byte[]? TryRenderThumbnail(string path)
    {
        if (_thumbnailRenderer is null)
        {
            return null;
        }

        try
        {
            return _thumbnailRenderer.Render(path, 128);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>A subscriber that throws must not take the migration down with it.</summary>
    private void Raise<T>(EventHandler<T>? handler, T args)
    {
        try
        {
            handler?.Invoke(this, args);
        }
        catch (Exception)
        {
            // Deliberately ignored: host callbacks are not part of the migration's success.
        }
    }

    private static bool IsImage(string path) =>
        ImageExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
}
