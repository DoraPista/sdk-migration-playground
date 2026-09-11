using System.Net.Http.Headers;
using System.Security.Cryptography;

namespace MigrationKit.Transfer;

public sealed record UploadReceipt(string Name, long Length, string Sha256);

public sealed class UploadService
{
    private readonly HttpClient _http;

    public UploadService(HttpClient http)
    {
        _http = http;
    }

    /// <summary>Kept for existing callers; path-based callers are the common desktop case.</summary>
    public Task<UploadReceipt> UploadAsync(string migrationId, string path, CancellationToken cancellationToken = default) =>
        UploadAsync(migrationId, SourceFile.FromPath(path), cancellationToken);

    public async Task<UploadReceipt> UploadAsync(string migrationId, SourceFile source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        // Pass 1: hash (and learn the real length if the source didn't know it).
        string sha256;
        long length;
        await using (var hashStream = await source.OpenReadAsync(cancellationToken))
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[81920];
            length = 0;
            int read;
            while ((read = await hashStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                hash.AppendData(buffer, 0, read);
                length += read;
            }

            sha256 = Convert.ToHexStringLower(hash.GetHashAndReset());
        }

        // Pass 2: stream the upload. A fresh stream per attempt makes retries possible.
        await using var content = await source.OpenReadAsync(cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"migrations/{migrationId}/files")
        {
            Content = new StreamContent(content),
        };
        request.Content.Headers.ContentLength = length;
        if (source.ContentType is not null)
        {
            request.Content.Headers.ContentType = new MediaTypeHeaderValue(source.ContentType);
        }

        request.Headers.Add("X-File-Name", Uri.EscapeDataString(source.Name));
        request.Headers.Add("X-Content-SHA256", sha256);

        using var response = await _http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return new UploadReceipt(source.Name, length, sha256);
    }
}
