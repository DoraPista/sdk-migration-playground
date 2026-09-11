using System.Text;

namespace MigrationKit.Transfer;

// Small internal helpers for APIs that .NET Standard 2.0 does not have.
// Keep them internal: they are an implementation detail, not part of the SDK's surface.

internal static class Hex
{
    /// <summary>Replaces Convert.ToHexStringLower (.NET 9+).</summary>
    public static string ToLower(byte[] bytes)
    {
        var text = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes)
        {
            text.Append(b.ToString("x2"));
        }

        return text.ToString();
    }
}

internal static class PathCompat
{
    /// <summary>
    /// Replaces Path.GetRelativePath (.NET Core 2.0 / .NET Standard 2.1+).
    /// Same behaviour for the cases this library needs: a full path below a root.
    /// </summary>
    public static string GetRelativePath(string root, string fullPath)
    {
        var normalisedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (fullPath.StartsWith(normalisedRoot, StringComparison.OrdinalIgnoreCase))
        {
            return fullPath.Substring(normalisedRoot.Length);
        }

        // Fall back to URI maths for paths that are not a simple prefix (e.g. "..\" segments).
        var rootUri = new Uri(normalisedRoot);
        var fileUri = new Uri(fullPath);
        return Uri.UnescapeDataString(rootUri.MakeRelativeUri(fileUri).ToString()).Replace('/', Path.DirectorySeparatorChar);
    }
}

internal static class EnumerableCompat
{
    /// <summary>Replaces Enumerable.Chunk (.NET 6+).</summary>
    public static IEnumerable<T[]> Chunk<T>(IEnumerable<T> source, int size)
    {
        var batch = new List<T>(size);
        foreach (var item in source)
        {
            batch.Add(item);
            if (batch.Count == size)
            {
                yield return batch.ToArray();
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            yield return batch.ToArray();
        }
    }
}
