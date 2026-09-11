namespace MigrationKit.Transfer;

/// <summary>Length is null when the source could not report it before being read.</summary>
public sealed record ManifestEntry(string Name, long? Length, DateTimeOffset? LastModified);

public sealed record MigrationManifest(IReadOnlyList<ManifestEntry> Entries)
{
    public long KnownBytes => Entries.Sum(e => e.Length ?? 0);

    public bool HasUnknownLengths => Entries.Any(e => e.Length is null);

    public long TotalBytes => KnownBytes;
}

public sealed class MigrationManifestBuilder
{
    public MigrationManifest Build(IEnumerable<string> paths) => Build(paths.Select(SourceFile.FromPath));

    /// <summary>Builds from metadata only; no source is opened.</summary>
    public MigrationManifest Build(IEnumerable<SourceFile> sources) =>
        new(sources.Select(s => new ManifestEntry(s.Name, s.Length, s.LastModified)).ToList());
}
