using System.Security.Cryptography;
using System.Text;

namespace Gym.TestUtilities;

/// <summary>
/// A throw-away directory of deterministic test files. Content is generated from a seed,
/// so the same call always produces the same bytes (and the same SHA-256).
/// </summary>
public sealed class TestFiles : IDisposable
{
    public TestFiles()
    {
        Root = Path.Combine(Path.GetTempPath(), "gym-temp", Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    public string PathOf(string relativePath) => Path.Combine(Root, relativePath);

    public string Create(string relativePath, long sizeBytes, int seed = 1)
    {
        var path = PathOf(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16);
        var random = new Random(seed);
        var buffer = new byte[1 << 16];
        var remaining = sizeBytes;
        while (remaining > 0)
        {
            var count = (int)Math.Min(buffer.Length, remaining);
            random.NextBytes(buffer.AsSpan(0, count));
            stream.Write(buffer, 0, count);
            remaining -= count;
        }

        return path;
    }

    public string CreateText(string relativePath, string content)
    {
        var path = PathOf(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, new UTF8Encoding(false));
        return path;
    }

    /// <summary>Creates <paramref name="count"/> files named file-0001.bin … with small varied sizes.</summary>
    public IReadOnlyList<string> CreateMany(int count, int minBytes = 1_000, int maxBytes = 20_000, string folder = "batch")
    {
        var sizes = new Random(count);
        return Enumerable.Range(1, count)
            .Select(i => Create(Path.Combine(folder, $"file-{i:D4}.bin"), sizes.Next(minBytes, maxBytes), seed: i))
            .ToArray();
    }

    public static string Sha256Hex(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    public static string Sha256Hex(byte[] content) => Convert.ToHexStringLower(SHA256.HashData(content));

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
            // A test that leaked a handle should fail on its own assertions, not in cleanup.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
