using System.Net;
using System.Security.Cryptography;

namespace MigrationKit.Chunked;

public sealed record ChunkUploadResult(int Chunks, long Bytes, string Sha256);

public sealed class ChunkedUploaderOptions
{
    public int ChunkSize { get; set; } = 64 * 1024;

    public int MaxAttemptsPerChunk { get; set; } = 3;

    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(200);
}

/// <summary>Uploads a file in chunks, retrying a chunk that fails with a transient error.</summary>
public sealed class ChunkedUploader
{
    private readonly HttpClient _http;
    private readonly ChunkedUploaderOptions _options;
    private readonly TimeProvider _time;

    public ChunkedUploader(HttpClient http, ChunkedUploaderOptions? options = null, TimeProvider? time = null)
    {
        _http = http;
        _options = options ?? new ChunkedUploaderOptions();
        _time = time ?? TimeProvider.System;
    }

    public async Task<ChunkUploadResult> UploadAsync(
        string uploadId,
        Stream content,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[_options.ChunkSize];
        long total = 0;
        var chunks = 0;
        int read;

        while ((read = await content.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
        {
            hash.AppendData(buffer, 0, read);
            await SendChunkAsync(uploadId, buffer.AsMemory(0, read).ToArray(), total, cancellationToken);

            total += read;
            chunks++;
            progress?.Report(total);
        }

        return new ChunkUploadResult(chunks, total, Convert.ToHexStringLower(hash.GetHashAndReset()));
    }

    private async Task SendChunkAsync(string uploadId, byte[] chunk, long offset, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Put, $"uploads/{uploadId}?offset={offset}")
            {
                Content = new ByteArrayContent(chunk),
            };

            HttpResponseMessage? response = null;
            try
            {
                response = await _http.SendAsync(request, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }

                if ((int)response.StatusCode < 500 && response.StatusCode != HttpStatusCode.TooManyRequests)
                {
                    throw new HttpRequestException($"Chunk at offset {offset} was rejected: {(int)response.StatusCode}.", null, response.StatusCode);
                }
            }
            finally
            {
                response?.Dispose();
            }

            if (attempt >= _options.MaxAttemptsPerChunk)
            {
                throw new HttpRequestException($"Chunk at offset {offset} failed after {attempt} attempts.");
            }

            await Task.Delay(_options.RetryDelay, _time, cancellationToken);
        }
    }
}
