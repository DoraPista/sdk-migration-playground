using System.Collections.Concurrent;
using System.Net;
using Gym.TestUtilities;
using MigrationKit.Transfer;

namespace Ex0701.LargeFiles.Tests;

// One test class only: the memory test measures process-wide allocations, so nothing may run in parallel with it.
public sealed class LargeFileUploaderTests : IDisposable
{
    private const long OneMegabyte = 1024 * 1024;
    private readonly TestFiles _files = new();
    private readonly ScriptedHttpHandler _platform = new ScriptedHttpHandler().OtherwiseRespond(HttpStatusCode.Created, new { fileId = "file-00001" });

    [Fact]
    public async Task Upload_arrives_intact_with_a_correct_hash_header()
    {
        var path = _files.Create("survey/pointcloud-extract.bin", 640_000);

        var receipt = await Uploader().UploadAsync("mig-00102", path);

        var request = Assert.Single(_platform.Requests);
        Assert.Equal(TestFiles.Sha256Hex(path), request.Header("X-Content-SHA256"));
        Assert.Equal(request.BodySha256, request.Header("X-Content-SHA256"));
        Assert.Equal(640_000, request.BodyLength);
        Assert.Equal(new UploadReceipt("file-00001", 640_000, TestFiles.Sha256Hex(path)), receipt);
    }

    [Fact]
    public async Task Content_length_is_declared()
    {
        var path = _files.Create("scan.e57", 3 * OneMegabyte);

        await Uploader().UploadAsync("mig-00102", path);

        Assert.Equal((3 * OneMegabyte).ToString(), Assert.Single(_platform.Requests).Header("Content-Length"));
    }

    [Fact]
    public async Task Memory_use_does_not_grow_with_the_file_size()
    {
        var warmUp = _files.Create("warm-up.bin", OneMegabyte);
        var large = _files.Create("facade-north.e57", 128 * OneMegabyte);
        var uploader = Uploader();
        await uploader.UploadAsync("mig-00102", warmUp);

        var before = GC.GetTotalAllocatedBytes(precise: true);
        await uploader.UploadAsync("mig-00102", large);
        var allocated = GC.GetTotalAllocatedBytes(precise: true) - before;

        Assert.True(allocated < 16 * OneMegabyte, $"Uploading a 128 MB file allocated {allocated / OneMegabyte} MB.");
        Assert.Equal(TestFiles.Sha256Hex(large), _platform.Requests[^1].BodySha256);
    }

    [Fact]
    public async Task Progress_is_reported_while_the_file_uploads()
    {
        var path = _files.Create("scan.e57", 8 * OneMegabyte);
        var reports = new ConcurrentQueue<long>();

        await Uploader().UploadAsync("mig-00102", path, new InlineProgress(reports.Enqueue));

        var values = reports.ToArray();
        Assert.True(values.Length >= 4, $"Only {values.Length} progress report(s) for an 8 MB file.");
        Assert.Equal(values.Order(), values);
        Assert.Equal(8 * OneMegabyte, values[^1]);
    }

    public void Dispose() => _files.Dispose();

    private LargeFileUploader Uploader() => new(new HttpClient(_platform) { BaseAddress = new Uri("https://platform.test/") });

    private sealed class InlineProgress(Action<long> report) : IProgress<long>
    {
        public void Report(long value) => report(value);
    }
}
