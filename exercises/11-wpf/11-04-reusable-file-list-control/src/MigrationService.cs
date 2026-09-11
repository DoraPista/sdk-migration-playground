namespace MigrationKit.Wpf;

public sealed record FileItem(string Name, string Status, long SizeBytes);

/// <summary>The migration app's service singleton, initialised at start-up.</summary>
public sealed class MigrationService
{
    private static MigrationService? _instance;

    private readonly IReadOnlyList<FileItem> _files;

    private MigrationService(IReadOnlyList<FileItem> files) => _files = files;

    public static MigrationService Instance =>
        _instance ?? throw new InvalidOperationException("MigrationService has not been initialised.");

    public static void Initialise(IReadOnlyList<FileItem> files) => _instance = new MigrationService(files);

    public Task<IReadOnlyList<FileItem>> GetFilesAsync() => Task.FromResult(_files);
}
