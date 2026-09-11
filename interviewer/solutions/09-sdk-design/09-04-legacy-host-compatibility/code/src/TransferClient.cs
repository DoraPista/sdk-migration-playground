using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace MigrationKit.Transfer;

public sealed class TransferClient
{
    private readonly HttpClient _http;
    private readonly TimeProvider _time;

    public TransferClient(HttpClient http, TimeProvider? time = null)
    {
        _http = http;
        _time = time ?? TimeProvider.System; // from Microsoft.Bcl.TimeProvider on .NET Standard 2.0
    }

    /// <summary>Registers a batch of files with the platform before they are uploaded.</summary>
    public async Task<string> RegisterBatchAsync(string migrationId, IReadOnlyList<FileEntry> batch, CancellationToken cancellationToken = default)
    {
        if (batch is null)
        {
            throw new ArgumentNullException(nameof(batch)); // no ArgumentNullException.ThrowIfNull (.NET 6+)
        }

        var request = new BatchRequest(
            migrationId,
            _time.GetUtcNow(),
            batch.Select(e => new BatchItem(e.RelativePath, e.Size, e.Sha256, e.IsImage)).ToList());

        using var response = await _http.PostAsJsonAsync($"migrations/{migrationId}/batches", request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var accepted = await response.Content.ReadFromJsonAsync<BatchAccepted>(cancellationToken: cancellationToken).ConfigureAwait(false);
        return accepted!.BatchId;
    }

    /// <summary>Streams a file to the platform and returns how many bytes were sent.</summary>
    public async Task<long> SendAsync(string uploadUrl, string path, CancellationToken cancellationToken = default)
    {
        if (path is null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        var buffer = new byte[81920];
        long total = 0;
        int read;

        using var content = new MemoryStream();

        // Stream.ReadAsync(Memory<byte>) is .NET Standard 2.1+; the array overload is everywhere.
        while ((read = await file.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
        {
            await content.WriteAsync(buffer, 0, read, cancellationToken).ConfigureAwait(false);
            total += read;
        }

        content.Position = 0;
        using var request = new HttpRequestMessage(HttpMethod.Put, uploadUrl) { Content = new StreamContent(content) };
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        return total;
    }

    /// <summary>Platform file names are always forward-slashed, whatever the customer's file system uses.</summary>
    public static bool LooksLikeManifestPath(string path) =>
        path.IndexOf('\\') < 0 && path.IndexOf('/') >= 0; // string.Contains(char) is .NET Core 2.1+

    private sealed record BatchRequest(string MigrationId, DateTimeOffset CreatedAt, IReadOnlyList<BatchItem> Items);

    private sealed record BatchItem(string Path, long Size, string Sha256, [property: JsonPropertyName("image")] bool IsImage);

    private sealed record BatchAccepted(string BatchId);
}
