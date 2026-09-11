using System.IO.Compression;
using System.Net;
using System.Text;
using Gym.TestUtilities;
using MigrationKit.Transfer;

namespace Ex0102.SourceFiles.Tests;

public sealed class SourceFileTests : IDisposable
{
    private readonly TestFiles _files = new();

    [Fact]
    public async Task Local_file_is_uploaded_with_name_and_hash()
    {
        var path = _files.Create("spec.pdf", 50_000);
        var (service, handler) = CreateService();

        var receipt = await service.UploadAsync("mig-1", path);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("spec.pdf", request.Header("X-File-Name"));
        Assert.Equal(TestFiles.Sha256Hex(path), request.Header("X-Content-SHA256"));
        Assert.Equal(request.BodySha256, receipt.Sha256);
        Assert.Equal(50_000, receipt.Length);
    }

    [Fact]
    public async Task In_memory_report_is_uploaded_without_touching_disk()
    {
        var content = Encoding.UTF8.GetBytes("generated report");
        var (service, handler) = CreateService();

        await service.UploadAsync("mig-1", SourceFile.FromBytes("report.txt", content, "text/plain"));

        var request = Assert.Single(handler.Requests);
        Assert.Equal(TestFiles.Sha256Hex(content), request.BodySha256);
        Assert.Equal("report.txt", request.Header("X-File-Name"));
    }

    [Fact]
    public async Task Non_seekable_stream_source_with_unknown_length_is_uploaded()
    {
        var data = new byte[70_000];
        new Random(5).NextBytes(data);
        var opened = 0;
        var source = SourceFile.FromStreamFactory("picked.jpg", _ =>
        {
            opened++;
            return Task.FromResult<Stream>(new NonSeekableStream(new MemoryStream(data)));
        });
        var (service, handler) = CreateService();

        var receipt = await service.UploadAsync("mig-1", source);

        Assert.Equal(2, opened); // hash pass + upload pass
        Assert.Equal(70_000, receipt.Length);
        Assert.Equal(TestFiles.Sha256Hex(data), Assert.Single(handler.Requests).BodySha256);
    }

    [Fact]
    public async Task Zip_entry_is_uploaded_without_extracting()
    {
        var zipPath = _files.PathOf("archive.zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            await using var entry = zip.CreateEntry("docs/plan.txt").Open();
            await entry.WriteAsync(Encoding.UTF8.GetBytes("plan"));
        }

        var source = SourceFile.FromStreamFactory("plan.txt", _ =>
        {
            var archive = ZipFile.OpenRead(zipPath);
            return Task.FromResult<Stream>(new OwningStream(archive.GetEntry("docs/plan.txt")!.Open(), archive));
        });
        var (service, handler) = CreateService();

        await service.UploadAsync("mig-1", source);

        Assert.Equal(TestFiles.Sha256Hex(Encoding.UTF8.GetBytes("plan")), Assert.Single(handler.Requests).BodySha256);
        File.Delete(zipPath); // proves nothing kept the archive open
    }

    [Fact]
    public void Manifest_is_built_without_opening_any_source()
    {
        var opened = false;
        var lazy = SourceFile.FromStreamFactory("lazy.bin", _ =>
        {
            opened = true;
            return Task.FromResult<Stream>(Stream.Null);
        });

        var manifest = new MigrationManifestBuilder().Build([SourceFile.FromPath(_files.Create("a.bin", 10)), lazy]);

        Assert.False(opened);
        Assert.Equal(10, manifest.KnownBytes);
        Assert.True(manifest.HasUnknownLengths);
    }

    [Fact]
    public void Manifest_lists_local_files()
    {
        var a = _files.Create("a.bin", 10);
        var b = _files.Create("b.bin", 20);

        var manifest = new MigrationManifestBuilder().Build([a, b]);

        Assert.Equal(["a.bin", "b.bin"], manifest.Entries.Select(e => e.Name));
        Assert.Equal(30, manifest.TotalBytes);
    }

    public void Dispose() => _files.Dispose();

    private static (UploadService, ScriptedHttpHandler) CreateService()
    {
        var handler = new ScriptedHttpHandler().OtherwiseRespond(HttpStatusCode.Created);
        return (new UploadService(new HttpClient(handler) { BaseAddress = new Uri("https://api.test/") }), handler);
    }

    private sealed class NonSeekableStream(Stream inner) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            if (disposing) inner.Dispose();
            base.Dispose(disposing);
        }
    }

    private sealed class OwningStream(Stream inner, IDisposable owner) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
                owner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
