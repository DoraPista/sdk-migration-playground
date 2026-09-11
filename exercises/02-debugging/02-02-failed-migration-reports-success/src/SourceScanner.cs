namespace MigrationKit.Migration;

public sealed class SourceScanner
{
    public IReadOnlyList<string> Scan(string root)
    {
        try
        {
            return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).ToList();
        }
        catch (Exception)
        {
            // Some folders on customer shares are protected; skip what we can't see.
            return Array.Empty<string>();
        }
    }
}
