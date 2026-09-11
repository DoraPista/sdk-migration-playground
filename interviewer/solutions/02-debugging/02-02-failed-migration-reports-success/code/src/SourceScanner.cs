namespace MigrationKit.Migration;

public sealed class SourceScanner
{
    /// <summary>
    /// Lists every file under <paramref name="root"/>. Throws if the root itself can't be read:
    /// "I can't see the source" is not the same as "the source is empty".
    /// Sub-folders that can't be read are reported, not silently dropped.
    /// </summary>
    public ScanResult Scan(string root)
    {
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"The source folder '{root}' does not exist or is not reachable.");
        }

        var inaccessible = new List<string>();
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = false,
            AttributesToSkip = FileAttributes.System,
        };

        var files = new List<string>();
        var pending = new Stack<string>([root]);
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            try
            {
                files.AddRange(Directory.EnumerateFiles(directory, "*", new EnumerationOptions { AttributesToSkip = options.AttributesToSkip }));
                foreach (var sub in Directory.EnumerateDirectories(directory))
                {
                    pending.Push(sub);
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException && directory != root)
            {
                inaccessible.Add(directory);
            }
        }

        return new ScanResult(files, inaccessible);
    }
}

public sealed record ScanResult(IReadOnlyList<string> Files, IReadOnlyList<string> InaccessibleFolders);
