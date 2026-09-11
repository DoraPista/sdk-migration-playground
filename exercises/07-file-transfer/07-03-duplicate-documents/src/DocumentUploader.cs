using System.Net.Http.Json;
using System.Security.Cryptography;

namespace MigrationKit.Documents;

/// <summary>A document from the customer's document-management system.</summary>
/// <param name="DocumentId">Stable id in the DMS, e.g. "DOC-10231".</param>
public sealed record SourceDocument(string DocumentId, string FileName, byte[] Content);

public sealed class DocumentUploaderOptions
{
    public int MaxAttempts { get; set; } = 4;

    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(500);
}

public sealed class DocumentUploader
{
    private readonly HttpClient _http;
    private readonly DocumentUploaderOptions _options;

    public DocumentUploader(HttpClient http, DocumentUploaderOptions options)
    {
        _http = http;
        _options = options;
    }

    /// <summary>Uploads the document and returns the platform's file id.</summary>
    public async Task<string> UploadAsync(string migrationId, SourceDocument document, CancellationToken cancellationToken = default)
    {
        var sha256 = Convert.ToHexStringLower(SHA256.HashData(document.Content));

        for (var attempt = 1; ; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"migrations/{migrationId}/files")
            {
                Content = new ByteArrayContent(document.Content),
            };
            request.Headers.Add("X-File-Name", Uri.EscapeDataString(document.FileName));
            request.Headers.Add("X-Content-SHA256", sha256);
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

            try
            {
                using var response = await _http.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();
                var stored = await response.Content.ReadFromJsonAsync<StoredFile>(cancellationToken);
                return stored!.FileId;
            }
            catch (HttpRequestException) when (attempt < _options.MaxAttempts)
            {
                await Task.Delay(_options.RetryDelay, cancellationToken);
            }
        }
    }

    private sealed record StoredFile(string FileId);
}
