using System.Buffers;
using System.Net;
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
        // Pass 1: hash while streaming. The contract wants the hash in a header, so it must be known before the
        // body is sent. The cost is reading the file twice (disk I/O), in exchange for constant memory.
        string sha256;
        long length;
        await using (var hashStream = OpenForSequentialRead(path))
        {
            length = hashStream.Length;
            sha256 = Convert.ToHexStringLower(await SHA256.HashDataAsync(hashStream, cancellationToken).ConfigureAwait(false));
        }

        // Pass 2: stream the body. Nothing is buffered beyond one small, pooled chunk.
        await using var body = OpenForSequentialRead(path);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"migrations/{migrationId}/files")
        {
            Content = new ProgressStreamContent(body, length, progress),
        };
        request.Headers.Add("X-File-Name", Uri.EscapeDataString(Path.GetFileName(path)));
        request.Headers.Add("X-Content-SHA256", sha256);

        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var stored = await response.Content.ReadFromJsonAsync<StoredFile>(cancellationToken).ConfigureAwait(false);
        return new UploadReceipt(stored!.FileId, length, sha256);
    }

    private static FileStream OpenForSequentialRead(string path) => new(path, new FileStreamOptions
    {
        Mode = FileMode.Open,
        Access = FileAccess.Read,
        Share = FileShare.Read,
        BufferSize = 0, // we read in large chunks ourselves; FileStream's own buffer would be a second copy
        Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
    });

    private sealed record StoredFile(string FileId);
}

/// <summary>Streams a source to the network in chunks, reporting progress as bytes are handed to the transport.</summary>
internal sealed class ProgressStreamContent(Stream source, long length, IProgress<long>? progress) : HttpContent
{
    private const int ChunkSize = 128 * 1024;

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
        SerializeToStreamAsync(stream, context, CancellationToken.None);

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(ChunkSize);
        try
        {
            long sent = 0;
            int read;
            while ((read = await source.ReadAsync(buffer.AsMemory(0, ChunkSize), cancellationToken).ConfigureAwait(false)) > 0)
            {
                await stream.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                sent += read;
                progress?.Report(sent);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>Declaring the length avoids chunked transfer encoding (rejected by the platform's gateway).</summary>
    protected override bool TryComputeLength(out long computedLength)
    {
        computedLength = length;
        return true;
    }
}
