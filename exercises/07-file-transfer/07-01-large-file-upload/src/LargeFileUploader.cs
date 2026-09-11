using System.Net.Http.Json;
using System.Security.Cryptography;

namespace MigrationKit.Transfer;

public sealed record UploadReceipt(string FileId, long Size, string Sha256);

public sealed class LargeFileUploader
{
    private readonly HttpClient _http;

    public LargeFileUploader(HttpClient http)
    {
        _http = http;
    }

    /// <param name="progress">Receives the number of bytes uploaded so far.</param>
    public async Task<UploadReceipt> UploadAsync(string migrationId, string path, IProgress<long>? progress = null, CancellationToken cancellationToken = default)
    {
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        var sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));

        using var request = new HttpRequestMessage(HttpMethod.Post, $"migrations/{migrationId}/files")
        {
            Content = new ByteArrayContent(bytes),
        };
        request.Headers.Add("X-File-Name", Uri.EscapeDataString(Path.GetFileName(path)));
        request.Headers.Add("X-Content-SHA256", sha256);

        using var response = await _http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        progress?.Report(bytes.Length);

        var stored = await response.Content.ReadFromJsonAsync<StoredFile>(cancellationToken);
        return new UploadReceipt(stored!.FileId, bytes.Length, sha256);
    }

    private sealed record StoredFile(string FileId);
}
