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

    public async Task<UploadReceipt> UploadAsync(string migrationId, string path, CancellationToken cancellationToken = default)
    {
        var info = new FileInfo(path);

        string sha256;
        using (var hashStream = File.OpenRead(path))
        {
            sha256 = Convert.ToHexStringLower(await SHA256.HashDataAsync(hashStream, cancellationToken));
        }

        using var content = File.OpenRead(path);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"migrations/{migrationId}/files")
        {
            Content = new StreamContent(content),
        };
        request.Headers.Add("X-File-Name", Uri.EscapeDataString(info.Name));
        request.Headers.Add("X-Content-SHA256", sha256);

        using var response = await _http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return new UploadReceipt(info.Name, info.Length, sha256);
    }
}
