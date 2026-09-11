using System.Security.Cryptography;

namespace MigrationKit.Auth;

/// <summary>Uploads small metadata files through an HttpClient that uses <see cref="AuthenticatingHandler"/>.</summary>
public sealed class FileUploader
{
    private readonly HttpClient _platform;

    public FileUploader(HttpClient platform)
    {
        _platform = platform;
    }

    public async Task UploadAsync(string migrationId, string name, byte[] content, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"migrations/{migrationId}/files")
        {
            Content = new ByteArrayContent(content),
        };
        request.Headers.Add("X-File-Name", Uri.EscapeDataString(name));
        request.Headers.Add("X-Content-SHA256", Convert.ToHexStringLower(SHA256.HashData(content)));

        using var response = await _platform.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
