namespace MigrationKit.Core;

/// <summary>A snapshot of a migration's progress.</summary>
public sealed record MigrationProgress(int FilesCompleted, int FilesTotal, string? CurrentFile);

public sealed record FileOutcome(string Path, bool Succeeded, string? Error, byte[]? Thumbnail);

public sealed record MigrationOutcome(bool Succeeded, IReadOnlyList<FileOutcome> Files);

/// <summary>Uploads one file to the platform.</summary>
public interface IFileUploader
{
    Task UploadAsync(string path, CancellationToken cancellationToken);
}

/// <summary>
/// Renders a thumbnail for an image, as PNG bytes. Implemented by the host:
/// the WPF app uses WPF imaging, the MAUI app uses its platform's.
/// </summary>
public interface IThumbnailRenderer
{
    byte[] Render(string imagePath, int maxSize);
}
