using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;

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

        // The key identifies the INTENT ("store this version of this document in this migration"), not the attempt.
        // It is derived, not random, so every retry AND every re-run after a crash sends the same key:
        //   * same document, same content, same migration -> same key -> the platform returns the original result
        //   * a different document with the same name     -> different DocumentId -> different key
        //   * an edited document                          -> different content hash -> different key (new version)
        var idempotencyKey = DeriveKey(migrationId, document.DocumentId, sha256);

        for (var attempt = 1; ; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"migrations/{migrationId}/files")
            {
                Content = new ByteArrayContent(document.Content),
            };
            request.Headers.Add("X-File-Name", Uri.EscapeDataString(document.FileName));
            request.Headers.Add("X-Content-SHA256", sha256);
            request.Headers.Add("Idempotency-Key", idempotencyKey);

            try
            {
                using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                var stored = await response.Content.ReadFromJsonAsync<StoredFile>(cancellationToken).ConfigureAwait(false);
                return stored!.FileId;
            }
            catch (HttpRequestException ex) when (attempt < _options.MaxAttempts && IsRetryable(ex))
            {
                await Task.Delay(_options.RetryDelay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    internal static string DeriveKey(string migrationId, string documentId, string sha256)
    {
        // Length-prefixed parts avoid ambiguity ("a:bc" vs "ab:c"); hashing keeps the key short and opaque.
        var material = $"{migrationId.Length}:{migrationId}|{documentId.Length}:{documentId}|{sha256}";
        return "doc-" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(material)))[..40];
    }

    private static bool IsRetryable(HttpRequestException ex) =>
        ex.StatusCode is null || (int)ex.StatusCode >= 500 || (int)ex.StatusCode == 429;

    private sealed record StoredFile(string FileId);
}
