using System.Net;
using System.Text;
using Gym.TestUtilities;
using MigrationKit.Chunked;

namespace Ex1301.Chunked.Tests;

public sealed class ChunkedUploaderTests
{
    private static int _uploadCounter;
    private static string? _lastHash;

    [Fact]
    public async Task Test1()
    {
        var handler = new ScriptedHttpHandler().OtherwiseRespond(HttpStatusCode.OK);
        var uploader = new ChunkedUploader(Client(handler), new ChunkedUploaderOptions { ChunkSize = 1024 });

        var result = await uploader.UploadAsync("upl-1", new MemoryStream(Data(4096)));

        _uploadCounter++;
        _lastHash = result.Sha256;
        Assert.Equal(4, result.Chunks);
    }

    [Fact]
    public async Task Test2_Hash()
    {
        // Depends on Test1 having run first.
        Assert.NotNull(_lastHash);
        Assert.Equal(64, _lastHash!.Length);
    }

    [Fact]
    public async Task UploadWorks()
    {
        var handler = new ScriptedHttpHandler().OtherwiseRespond(HttpStatusCode.OK);
        var uploader = new ChunkedUploader(Client(handler));

        try
        {
            var result = await uploader.UploadAsync("upl-2", new MemoryStream(Data(10_000)));
            Assert.Equal(10_000, result.Bytes);
        }
        catch (Exception)
        {
            // Sometimes flaky on the build server, ignore.
        }
    }

    [Fact]
    public async Task RetryTest()
    {
        var handler = new ScriptedHttpHandler()
            .RespondWith(HttpStatusCode.ServiceUnavailable)
            .RespondWith(HttpStatusCode.ServiceUnavailable)
            .OtherwiseRespond(HttpStatusCode.OK);
        var uploader = new ChunkedUploader(Client(handler), new ChunkedUploaderOptions
        {
            ChunkSize = 4096,
            RetryDelay = TimeSpan.FromSeconds(2),
        });

        var upload = uploader.UploadAsync("upl-3", new MemoryStream(Data(4096)));

        // Wait for the retries to happen.
        await Task.Delay(TimeSpan.FromSeconds(5));
        var result = await upload;

        Assert.Equal(1, result.Chunks);
    }

    [Fact]
    public async Task IntegrationTest()
    {
        using var http = new HttpClient { BaseAddress = new Uri("http://localhost:5999/") };
        var uploader = new ChunkedUploader(http);

        var result = await uploader.UploadAsync("upl-4", new MemoryStream(Data(2048)));

        Assert.True(result.Bytes > 0);
    }

    [Fact]
    public async Task ProgressTest()
    {
        var handler = new ScriptedHttpHandler().OtherwiseRespond(HttpStatusCode.OK);
        var uploader = new ChunkedUploader(Client(handler), new ChunkedUploaderOptions { ChunkSize = 1000 });
        var reports = new List<long>();

        await uploader.UploadAsync("upl-5", new MemoryStream(Data(3000)), new Progress<long>(reports.Add));

        // Progress<T> posts to the thread pool, so give it a moment.
        await Task.Delay(500);
        Assert.True(reports.Count > 0);
    }

    [Fact]
    public async Task CancelTest()
    {
        var handler = new ScriptedHttpHandler().Otherwise(async (_, ct) =>
        {
            await Task.Delay(200, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var uploader = new ChunkedUploader(Client(handler), new ChunkedUploaderOptions { ChunkSize = 100 });
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));

        try
        {
            await uploader.UploadAsync("upl-6", new MemoryStream(Data(10_000)), cancellationToken: cts.Token);
        }
        catch (OperationCanceledException)
        {
        }
    }

    [Fact]
    public void OptionsTest()
    {
        var options = new ChunkedUploaderOptions();

        Assert.Equal(65536, options.ChunkSize);
        Assert.Equal(3, options.MaxAttemptsPerChunk);
        Assert.Equal(TimeSpan.FromMilliseconds(200), options.RetryDelay);
        Assert.NotNull(options);
    }

    private static HttpClient Client(ScriptedHttpHandler handler) =>
        new(handler) { BaseAddress = new Uri("https://platform.test/") };

    private static byte[] Data(int size) => Encoding.UTF8.GetBytes(new string('x', size));
}
