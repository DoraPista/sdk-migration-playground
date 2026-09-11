using Microsoft.Extensions.Logging;

namespace MigrationKit.Diagnostics;

/// <summary>Uploads file content directly to a pre-signed (SAS) storage URL handed out by the platform.</summary>
public sealed class BlobUploader
{
    private readonly HttpClient _http;
    private readonly ILogger<BlobUploader> _logger;

    public BlobUploader(HttpClient http, ILogger<BlobUploader> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task UploadAsync(Uri sasUrl, Stream content, CancellationToken cancellationToken = default)
    {
        // A SAS URL is a bearer credential: anyone holding it can write here until it expires.
        var safeUrl = Redaction.SafeUrl(sasUrl);
        _logger.LogInformation("Uploading {Bytes} bytes to {Url}", content.Length, safeUrl);

        using var request = new HttpRequestMessage(HttpMethod.Put, sasUrl) { Content = new StreamContent(content) };
        request.Headers.Add("x-ms-blob-type", "BlockBlob");

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            // Hosts log exceptions (often with ToString()), so exception messages must be safe too.
            var error = new HttpRequestException($"Upload to {safeUrl} failed with {(int)response.StatusCode}.", null, response.StatusCode);
            _logger.LogError(error, "Blob upload to {Url} failed with {Status}", safeUrl, (int)response.StatusCode);
            throw error;
        }
    }
}
