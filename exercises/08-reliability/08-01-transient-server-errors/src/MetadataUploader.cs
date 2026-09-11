using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;

namespace MigrationKit.Resilience;

public sealed class PlatformRequestException(string message, HttpStatusCode? statusCode, Exception? innerException = null)
    : Exception(message, innerException)
{
    /// <summary>The HTTP status, or null if no response was received.</summary>
    public HttpStatusCode? StatusCode { get; } = statusCode;
}

public sealed class MetadataUploader
{
    private readonly HttpClient _http;
    private readonly TimeProvider _time;

    public MetadataUploader(HttpClient http, TimeProvider? time = null)
    {
        _http = http;
        _time = time ?? TimeProvider.System;
    }

    /// <summary>Uploads a project's metadata document and returns the platform's file id.</summary>
    public async Task<string> UploadAsync(string migrationId, string projectId, byte[] metadataJson, CancellationToken cancellationToken = default)
    {
        var sha256 = Convert.ToHexStringLower(SHA256.HashData(metadataJson));

        using var request = new HttpRequestMessage(HttpMethod.Post, $"migrations/{migrationId}/files?projectId={Uri.EscapeDataString(projectId)}")
        {
            Content = new ByteArrayContent(metadataJson),
        };
        request.Headers.Add("X-File-Name", Uri.EscapeDataString($"metadata/{projectId}.json"));
        request.Headers.Add("X-Content-SHA256", sha256);
        request.Headers.Add("Idempotency-Key", $"meta-{migrationId}-{projectId}-{sha256[..16]}");

        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new PlatformRequestException($"Metadata upload failed with {(int)response.StatusCode}.", response.StatusCode);
        }

        var stored = await response.Content.ReadFromJsonAsync<StoredFile>(cancellationToken);
        return stored!.FileId;
    }

    private sealed record StoredFile(string FileId);
}
