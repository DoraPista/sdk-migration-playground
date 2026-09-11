using System.Security.Cryptography;

namespace MigrationKit.Transfer;

public static class Hashing
{
    /// <summary>SHA-256 of a file, as lower-case hex.</summary>
    public static async Task<string> Sha256Async(string path, CancellationToken cancellationToken = default)
    {
        // No SHA256.HashDataAsync and no await using (FileStream is not IAsyncDisposable) on .NET Standard 2.0.
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        var buffer = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
        {
            hash.AppendData(buffer, 0, read);
        }

        return Hex.ToLower(hash.GetHashAndReset());
    }

    public static string Sha256(byte[] content)
    {
        using var sha = SHA256.Create();
        return Hex.ToLower(sha.ComputeHash(content));
    }
}
