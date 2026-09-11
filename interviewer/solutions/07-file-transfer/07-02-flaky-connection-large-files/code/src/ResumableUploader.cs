using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

namespace MigrationKit.Transfer;

public sealed class ResumableUploadOptions
{
    /// <summary>Give up after this many consecutive attempts that make no progress.</summary>
    public int MaxAttemptsWithoutProgress { get; set; } = 3;

    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(2);
}

public sealed record StoredFile(string FileId, string Name, long Size, string Sha256);

internal sealed record UploadSession(string UploadId, long Length, long Received, bool Completed);

public sealed class ResumableUploader
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;
    private readonly ResumableUploadOptions _options;

    public ResumableUploader(HttpClient http, ResumableUploadOptions options)
    {
        _http = http;
        _options = options;
    }

    public async Task<StoredFile> UploadAsync(string migrationId, string path, CancellationToken cancellationToken = default)
    {
        var fileName = Path.GetFileName(path);
        var length = new FileInfo(path).Length;
        var sha256 = await ComputeSha256Async(path, cancellationToken).ConfigureAwait(false);

        // One session per file. The SERVER is the source of truth for how much it has: after any failure we ask
        // it, instead of assuming (the client can't know how many of the bytes it wrote actually arrived).
        var session = await StartSessionAsync(migrationId, fileName, length, sha256, cancellationToken).ConfigureAwait(false);
        var confirmed = 0L;
        var attemptsWithoutProgress = 0;

        while (true)
        {
            try
            {
                if (confirmed < length)
                {
                    confirmed = await PutRemainderAsync(session.UploadId, path, confirmed, length, cancellationToken).ConfigureAwait(false);
                }

                if (confirmed == length)
                {
                    return await CompleteAsync(session.UploadId, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (IsTransient(ex) && !cancellationToken.IsCancellationRequested)
            {
                var before = confirmed;
                var serverView = await TryGetSessionAsync(session.UploadId, cancellationToken).ConfigureAwait(false);
                switch (serverView)
                {
                    case { Found: false }:
                        // The platform forgot the session (restart, expiry): start over with a fresh one.
                        session = await StartSessionAsync(migrationId, fileName, length, sha256, cancellationToken).ConfigureAwait(false);
                        confirmed = 0;
                        break;
                    case { Found: true, Session: { } known }:
                        confirmed = known.Received;
                        break;
                    default:
                        break; // couldn't reach the server either; keep what we knew
                }

                attemptsWithoutProgress = confirmed > before ? 0 : attemptsWithoutProgress + 1;
                if (attemptsWithoutProgress >= _options.MaxAttemptsWithoutProgress)
                {
                    throw;
                }

                await Task.Delay(_options.RetryDelay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Sends everything from <paramref name="offset"/> to the end; returns the server's new received count.</summary>
    private async Task<long> PutRemainderAsync(string uploadId, string path, long offset, long length, CancellationToken cancellationToken)
    {
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        file.Seek(offset, SeekOrigin.Begin);

        using var request = new HttpRequestMessage(HttpMethod.Put, $"uploads/{uploadId}") { Content = new StreamContent(file, 128 * 1024) };
        request.Content.Headers.ContentRange = new ContentRangeHeaderValue(offset, length - 1, length);

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            // Our offset was wrong (e.g. a previous attempt got further than we thought): the body says where to go on.
            var actual = await response.Content.ReadFromJsonAsync<UploadSession>(Json, cancellationToken).ConfigureAwait(false);
            return actual!.Received;
        }

        response.EnsureSuccessStatusCode();
        var session = await response.Content.ReadFromJsonAsync<UploadSession>(Json, cancellationToken).ConfigureAwait(false);
        return session!.Received;
    }

    private async Task<(bool Found, UploadSession? Session)?> TryGetSessionAsync(string uploadId, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _http.GetAsync($"uploads/{uploadId}", cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return (false, null);
            }

            response.EnsureSuccessStatusCode();
            return (true, await response.Content.ReadFromJsonAsync<UploadSession>(Json, cancellationToken).ConfigureAwait(false));
        }
        catch (Exception ex) when (IsTransient(ex) && !cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private async Task<UploadSession> StartSessionAsync(string migrationId, string fileName, long length, string sha256, CancellationToken cancellationToken)
    {
        using var response = await _http.PostAsJsonAsync($"migrations/{migrationId}/uploads", new { fileName, length, sha256 }, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UploadSession>(Json, cancellationToken).ConfigureAwait(false))!;
    }

    private async Task<StoredFile> CompleteAsync(string uploadId, CancellationToken cancellationToken)
    {
        using var response = await _http.PostAsync($"uploads/{uploadId}/complete", null, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<StoredFile>(Json, cancellationToken).ConfigureAwait(false))!;
    }

    private static bool IsTransient(Exception ex) => ex switch
    {
        HttpRequestException { StatusCode: null } => true,                          // connection reset, DNS, ...
        HttpRequestException { StatusCode: { } status } => (int)status >= 500 || status == HttpStatusCode.TooManyRequests,
        IOException => true,
        OperationCanceledException => true,                                          // HttpClient.Timeout (caller token checked separately)
        _ => false,
    };

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
    }
}
