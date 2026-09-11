using System.Security.Cryptography;

namespace MigrationKit.Transfer;

public sealed class FileUploader
{
    private readonly HttpClient _http;

    public FileUploader(HttpClient http)
    {
        _http = http;
    }

    public async Task UploadAsync(string migrationId, string path, CancellationToken cancellationToken = default)
    {
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);

        var hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken));
        stream.Position = 0;

        var request = new HttpRequestMessage(HttpMethod.Post, $"migrations/{migrationId}/files")
        {
            Content = new StreamContent(stream),
        };
        request.Headers.Add("X-File-Name", Uri.EscapeDataString(Path.GetFileName(path)));
        request.Headers.Add("X-Content-SHA256", hash);

        var response = await _http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        stream.Dispose();
    }
}
