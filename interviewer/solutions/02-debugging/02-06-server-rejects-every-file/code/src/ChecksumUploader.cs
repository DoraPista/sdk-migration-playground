using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;

namespace MigrationKit.Upload;

public sealed class UploadFailedException(string path, HttpStatusCode statusCode, string serverMessage)
    : Exception($"Uploading '{Path.GetFileName(path)}' failed with {(int)statusCode}: {serverMessage}")
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}

public sealed class ChecksumUploader
{
    private readonly HttpClient _http;
    private readonly int _maxAttempts;

    public ChecksumUploader(HttpClient http, int maxAttempts = 3)
    {
        _http = http;
        _maxAttempts = maxAttempts;
    }

    /// <summary>Uploads a file and returns the server's file id.</summary>
    public async Task<string> UploadAsync(string migrationId, string path, CancellationToken cancellationToken = default)
    {
        // Pass 1: the hash. Reading the stream to the end leaves Position == Length.
        string sha256;
        await using (var hashStream = File.OpenRead(path))
        {
            sha256 = Convert.ToHexStringLower(await SHA256.HashDataAsync(hashStream, cancellationToken));
        }

        for (var attempt = 1; ; attempt++)
        {
            // Pass 2..n: a NEW request (and a new stream from offset 0) for every attempt.
            // An HttpRequestMessage can only be sent once, and its content has been consumed.
            await using var content = File.OpenRead(path);
            using var request = new HttpRequestMessage(HttpMethod.Post, $"migrations/{migrationId}/files")
            {
                Content = new StreamContent(content),
            };
            request.Headers.Add("X-File-Name", Uri.EscapeDataString(Path.GetFileName(path)));
            request.Headers.Add("X-Content-SHA256", sha256);

            using var response = await _http.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var stored = await response.Content.ReadFromJsonAsync<StoredFile>(cancellationToken);
                return stored!.FileId;
            }

            if ((int)response.StatusCode >= 500 && attempt < _maxAttempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt), cancellationToken);
                continue;
            }

            throw new UploadFailedException(path, response.StatusCode, await response.Content.ReadAsStringAsync(cancellationToken));
        }
    }

    private sealed record StoredFile(string FileId, string Name, long Size, string Sha256);
}
