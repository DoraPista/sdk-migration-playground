using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

namespace MigrationKit.Transfer;

public sealed class ResumableUploadOptions
{
    /// <summary>Give up after this many consecutive attempts that make no progress.</summary>
    public int MaxAttemptsWithoutProgress { get; set; } = 3;

    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(2);
}

public sealed record StoredFile(string FileId, string Name, long Size, string Sha256);

internal sealed record UploadSession(string UploadId, long Length, long Received, bool Completed);

public sealed class ResumableUploader
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;
    private readonly ResumableUploadOptions _options;

    public ResumableUploader(HttpClient http, ResumableUploadOptions options)
    {
        _http = http;
        _options = options;
    }

    public async Task<StoredFile> UploadAsync(string migrationId, string path, CancellationToken cancellationToken = default)
    {
        var length = new FileInfo(path).Length;
        var sha256 = await ComputeSha256Async(path, cancellationToken);

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var session = await StartSessionAsync(migrationId, Path.GetFileName(path), length, sha256, cancellationToken);

                await using var file = File.OpenRead(path);
                await PutRangeAsync(session.UploadId, file, offset: 0, length, cancellationToken);

                return await CompleteAsync(session.UploadId, cancellationToken);
            }
            catch (HttpRequestException) when (attempt < _options.MaxAttemptsWithoutProgress)
            {
                await Task.Delay(_options.RetryDelay, cancellationToken);
            }
        }
    }

    private async Task<UploadSession> StartSessionAsync(string migrationId, string fileName, long length, string sha256, CancellationToken cancellationToken)
    {
        using var response = await _http.PostAsJsonAsync($"migrations/{migrationId}/uploads", new { fileName, length, sha256 }, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UploadSession>(Json, cancellationToken))!;
    }

    private async Task PutRangeAsync(string uploadId, Stream file, long offset, long length, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"uploads/{uploadId}") { Content = new StreamContent(file) };
        request.Content.Headers.ContentRange = new ContentRangeHeaderValue(offset, length - 1, length);

        using var response = await _http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task<StoredFile> CompleteAsync(string uploadId, CancellationToken cancellationToken)
    {
        using var response = await _http.PostAsync($"uploads/{uploadId}/complete", null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<StoredFile>(Json, cancellationToken))!;
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken));
    }
}
