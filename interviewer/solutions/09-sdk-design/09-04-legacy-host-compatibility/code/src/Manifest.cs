using System.Text.Json;

namespace MigrationKit.Transfer;

/// <summary>One entry of a customer's export manifest.</summary>
public sealed record FileEntry(string RelativePath, long Size, string Sha256)
{
    /// <summary>Files the platform stores as images (thumbnails are generated for these).</summary>
    public bool IsImage => RelativePath.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                           || RelativePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase);
}

public static class ManifestReader
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task<IReadOnlyList<FileEntry>> ReadAsync(string manifestPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // No File.ReadAllTextAsync on .NET Standard 2.0; read the stream asynchronously instead.
        using var stream = new FileStream(manifestPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        return await JsonSerializer.DeserializeAsync<List<FileEntry>>(stream, Json, cancellationToken).ConfigureAwait(false)
               ?? new List<FileEntry>();
    }

    /// <summary>Turns a full path into the manifest form: relative to the root, with forward slashes.</summary>
    public static string ToManifestPath(string root, string fullPath) =>
        PathCompat.GetRelativePath(root, fullPath).Replace('\\', '/');
}
