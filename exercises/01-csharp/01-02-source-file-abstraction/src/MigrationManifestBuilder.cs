namespace MigrationKit.Transfer;

public sealed record ManifestEntry(string Name, long Length, DateTimeOffset LastModified);

public sealed record MigrationManifest(IReadOnlyList<ManifestEntry> Entries)
{
    public long TotalBytes => Entries.Sum(e => e.Length);
}

public sealed class MigrationManifestBuilder
{
    public MigrationManifest Build(IEnumerable<string> paths)
    {
        var entries = new List<ManifestEntry>();
        foreach (var path in paths)
        {
            var info = new FileInfo(path);
            if (!info.Exists)
            {
                throw new FileNotFoundException("Source file not found.", path);
            }

            entries.Add(new ManifestEntry(info.Name, info.Length, info.LastWriteTimeUtc));
        }

        return new MigrationManifest(entries);
    }
}
