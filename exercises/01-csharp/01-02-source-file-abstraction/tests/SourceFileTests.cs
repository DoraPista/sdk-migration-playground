using System.Net;
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
        var handler = new ScriptedHttpHandler().OtherwiseRespond(HttpStatusCode.Created);
        var service = new UploadService(new HttpClient(handler) { BaseAddress = new Uri("https://api.test/") });

        var receipt = await service.UploadAsync("mig-1", path);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("spec.pdf", request.Header("X-File-Name"));
        Assert.Equal(TestFiles.Sha256Hex(path), request.Header("X-Content-SHA256"));
        Assert.Equal(request.BodySha256, receipt.Sha256);
        Assert.Equal(50_000, receipt.Length);
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
}
