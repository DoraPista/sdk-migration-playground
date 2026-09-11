using System.Net;
using System.Text;
using Gym.TestUtilities;
using Microsoft.Extensions.Time.Testing;
using MigrationKit.Chunked;

namespace Ex1301.Chunked.Tests;

/// <summary>
/// Deterministic, offline, order-independent, and each name says what it protects.
/// No shared state between tests, no sleeps, no network.
/// </summary>
public sealed class ChunkedUploaderTests
{
    private readonly ScriptedHttpHandler _platform = new();
    private readonly FakeTimeProvider _time = new();

    [Fact]
    public async Task Content_is_sent_in_chunks_of_the_configured_size()
    {
        _platform.OtherwiseRespond(HttpStatusCode.OK);

        var result = await Uploader(chunkSize: 1024).UploadAsync("upl-1", new MemoryStream(Data(4096)));

        Assert.Equal(4, result.Chunks);
        Assert.Equal(4096, result.Bytes);
        Assert.Equal(4, _platform.Requests.Count);
        Assert.All(_platform.Requests, r => Assert.Equal(1024, r.BodyLength));
    }

    [Fact]
    public async Task Each_chunk_carries_its_offset()
    {
        _platform.OtherwiseRespond(HttpStatusCode.OK);

        await Uploader(chunkSize: 1000).UploadAsync("upl-1", new MemoryStream(Data(2500)));

        Assert.Equal(["offset=0", "offset=1000", "offset=2000"], _platform.Requests.Select(r => r.Uri.Query.TrimStart('?')));
    }

    [Fact]
    public async Task The_result_hashes_the_whole_content()
    {
        _platform.OtherwiseRespond(HttpStatusCode.OK);
        var content = Data(5_000);

        var result = await Uploader(chunkSize: 512).UploadAsync("upl-1", new MemoryStream(content));

        Assert.Equal(TestFiles.Sha256Hex(content), result.Sha256);
    }

    [Fact]
    public async Task Empty_content_sends_nothing()
    {
        _platform.OtherwiseRespond(HttpStatusCode.OK);

        var result = await Uploader().UploadAsync("upl-1", new MemoryStream([]));

        Assert.Equal(0, result.Chunks);
        Assert.Empty(_platform.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task A_transient_failure_retries_the_same_chunk(HttpStatusCode transient)
    {
        _platform.RespondWith(transient).RespondWith(transient).OtherwiseRespond(HttpStatusCode.OK);
        var content = Data(4096);

        var upload = Uploader(chunkSize: 4096).UploadAsync("upl-1", new MemoryStream(content));
        await _time.AdvanceUntilCompleteAsync(upload, TimeSpan.FromSeconds(1));

        Assert.Equal(1, (await upload).Chunks);
        Assert.Equal(3, _platform.Requests.Count);
        Assert.Single(_platform.Requests.Select(r => r.BodySha256).Distinct()); // the same bytes every time
    }

    [Fact]
    public async Task Retries_wait_between_attempts()
    {
        _platform.RespondWith(HttpStatusCode.ServiceUnavailable).OtherwiseRespond(HttpStatusCode.OK);
        var started = _time.GetUtcNow();

        var upload = Uploader(chunkSize: 4096, retryDelay: TimeSpan.FromSeconds(5)).UploadAsync("upl-1", new MemoryStream(Data(4096)));
        await _time.AdvanceUntilCompleteAsync(upload, TimeSpan.FromSeconds(1));

        await upload;
        Assert.True(_time.GetUtcNow() - started >= TimeSpan.FromSeconds(5), "The retry did not wait.");
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    public async Task A_rejected_chunk_fails_without_retrying(HttpStatusCode permanent)
    {
        _platform.OtherwiseRespond(permanent);

        var error = await Assert.ThrowsAsync<HttpRequestException>(() =>
            Uploader(chunkSize: 4096).UploadAsync("upl-1", new MemoryStream(Data(4096))));

        Assert.Equal(permanent, error.StatusCode);
        Assert.Single(_platform.Requests);
    }

    [Fact]
    public async Task The_upload_gives_up_after_the_configured_number_of_attempts()
    {
        _platform.OtherwiseRespond(HttpStatusCode.ServiceUnavailable);

        var upload = Uploader(chunkSize: 4096, maxAttempts: 3).UploadAsync("upl-1", new MemoryStream(Data(4096)));
        await _time.AdvanceUntilCompleteAsync(upload, TimeSpan.FromSeconds(1)).ContinueWith(_ => { });

        await Assert.ThrowsAsync<HttpRequestException>(() => upload);
        Assert.Equal(3, _platform.Requests.Count);
    }

    [Fact]
    public async Task Progress_is_reported_after_every_chunk()
    {
        _platform.OtherwiseRespond(HttpStatusCode.OK);
        var reports = new List<long>();

        await Uploader(chunkSize: 1000).UploadAsync("upl-1", new MemoryStream(Data(3000)), new InlineProgress(reports.Add));

        Assert.Equal([1000L, 2000L, 3000L], reports);
    }

    [Fact]
    public async Task Cancelling_stops_the_upload_and_the_remaining_chunks()
    {
        using var cancellation = new CancellationTokenSource();
        _platform.Otherwise((_, _) =>
        {
            if (_platform.CallCount >= 2)
            {
                cancellation.Cancel();
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Uploader(chunkSize: 100).UploadAsync("upl-1", new MemoryStream(Data(10_000)), cancellationToken: cancellation.Token));

        Assert.InRange(_platform.Requests.Count, 1, 5); // not all 100 chunks
    }

    private ChunkedUploader Uploader(int chunkSize = 64 * 1024, int maxAttempts = 3, TimeSpan? retryDelay = null) =>
        new(
            new HttpClient(_platform) { BaseAddress = new Uri("https://platform.test/") },
            new ChunkedUploaderOptions
            {
                ChunkSize = chunkSize,
                MaxAttemptsPerChunk = maxAttempts,
                RetryDelay = retryDelay ?? TimeSpan.FromMilliseconds(200),
            },
            _time);

    private static byte[] Data(int size) => Encoding.UTF8.GetBytes(new string('x', size));

    private sealed class InlineProgress(Action<long> report) : IProgress<long>
    {
        public void Report(long value) => report(value);
    }
}
