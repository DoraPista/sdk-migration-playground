using System.Text.Json;

namespace MigrationKit.Resume;

public sealed record SourceFile(string FileId, string RelativePath, long SizeBytes);

public sealed record FileCheckpoint(string FileId, string RelativePath, string Status, string? RemoteFileId);

public sealed class MigrationCheckpoint
{
    public string MigrationId { get; set; } = string.Empty;

    public string CustomerId { get; set; } = string.Empty;

    public DateTimeOffset LastUpdated { get; set; }

    public int TotalFiles { get; set; }

    public List<FileCheckpoint> Files { get; set; } = new();
}

public sealed record MigrationSummary(string MigrationId, int FilesUploaded, int FilesSkipped);

public interface ICheckpointStore
{
    Task<MigrationCheckpoint?> LoadAsync(string customerId, CancellationToken cancellationToken);

    Task SaveAsync(MigrationCheckpoint checkpoint, CancellationToken cancellationToken);
}

public interface IMigrationPlatform
{
    Task<string> CreateMigrationAsync(string customerId, CancellationToken cancellationToken);

    /// <summary>The customer's migration that is still open, if there is one.</summary>
    Task<string?> FindOpenMigrationAsync(string customerId, CancellationToken cancellationToken);

    /// <summary>Uploads a file and returns the platform's file id.</summary>
    Task<string> UploadFileAsync(string migrationId, SourceFile file, CancellationToken cancellationToken);

    /// <summary>The file ids the platform has actually stored for this migration.</summary>
    Task<IReadOnlyList<string>> ListUploadedFileIdsAsync(string migrationId, CancellationToken cancellationToken);
}

/// <summary>One JSON file per customer, in the app's local data folder.</summary>
public sealed class JsonCheckpointStore : ICheckpointStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly string _directory;

    public JsonCheckpointStore(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(directory);
    }

    public string PathFor(string customerId) => Path.Combine(_directory, $"{customerId}.checkpoint.json");

    public async Task<MigrationCheckpoint?> LoadAsync(string customerId, CancellationToken cancellationToken)
    {
        var path = PathFor(customerId);
        if (!File.Exists(path))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        return JsonSerializer.Deserialize<MigrationCheckpoint>(json, Json);
    }

    /// <summary>
    /// Writes to a temporary file, flushes it to disk, then replaces the real one. A crash during a save
    /// therefore leaves either the previous checkpoint or the new one, never half of either.
    /// </summary>
    public async Task SaveAsync(MigrationCheckpoint checkpoint, CancellationToken cancellationToken)
    {
        var path = PathFor(checkpoint.CustomerId);
        var temp = path + ".tmp";

        await using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        {
            await JsonSerializer.SerializeAsync(stream, checkpoint, Json, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        File.Move(temp, path, overwrite: true);
    }
}
