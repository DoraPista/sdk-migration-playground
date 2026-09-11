using System.Text.Json;

namespace MigrationKit.State;

public sealed class MigrationProgressState
{
    public string MigrationId { get; set; } = string.Empty;

    public List<string> UploadedFileIds { get; set; } = new();

    public long BytesUploaded { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Durable storage for migration progress.</summary>
public interface IStateStore
{
    Task<MigrationProgressState> LoadAsync(string migrationId, CancellationToken cancellationToken);

    Task SaveAsync(MigrationProgressState state, CancellationToken cancellationToken);
}

/// <summary>The production store: one JSON file per migration.</summary>
public sealed class JsonFileStateStore : IStateStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly string _directory;

    public JsonFileStateStore(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(directory);
    }

    public async Task<MigrationProgressState> LoadAsync(string migrationId, CancellationToken cancellationToken)
    {
        var path = PathFor(migrationId);
        if (!File.Exists(path))
        {
            return new MigrationProgressState { MigrationId = migrationId };
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<MigrationProgressState>(stream, Json, cancellationToken)
               ?? new MigrationProgressState { MigrationId = migrationId };
    }

    public async Task SaveAsync(MigrationProgressState state, CancellationToken cancellationToken)
    {
        await using var stream = File.Create(PathFor(state.MigrationId));
        await JsonSerializer.SerializeAsync(stream, state, Json, cancellationToken);
    }

    private string PathFor(string migrationId) => Path.Combine(_directory, $"{migrationId}.state.json");
}
