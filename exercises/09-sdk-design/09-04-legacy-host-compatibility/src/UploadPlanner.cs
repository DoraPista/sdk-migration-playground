namespace MigrationKit.Transfer;

/// <summary>Groups the manifest into batches the platform accepts in one call.</summary>
public static class UploadPlanner
{
    public static IReadOnlyList<IReadOnlyList<FileEntry>> PlanBatches(IEnumerable<FileEntry> entries, int batchSize)
    {
        if (batchSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize), batchSize, "A batch must hold at least one file.");
        }

        return entries.Chunk(batchSize).Select(batch => (IReadOnlyList<FileEntry>)batch).ToList();
    }

    /// <summary>Total bytes of everything that still has to be transferred.</summary>
    public static long TotalBytes(IEnumerable<FileEntry> entries) => entries.Sum(e => e.Size);
}
