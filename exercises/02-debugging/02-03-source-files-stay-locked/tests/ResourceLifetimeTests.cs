using System.Net;
using Gym.TestUtilities;
using MigrationKit.Transfer;

namespace Ex0203.Transfer.Tests;

public sealed class ResourceLifetimeTests : IDisposable
{
    private readonly TestFiles _files = new();

    [Fact]
    public async Task Source_file_is_released_after_a_successful_upload()
    {
        var path = _files.Create("drawing.dwg", 20_000);
        var uploader = Uploader(new ScriptedHttpHandler().OtherwiseRespond(HttpStatusCode.Created));

        await uploader.UploadAsync("mig-1", path);

        AssertNotLocked(path);
    }

    [Fact]
    public async Task Source_file_is_released_after_a_failed_upload()
    {
        var path = _files.Create("drawing.dwg", 20_000);
        var uploader = Uploader(new ScriptedHttpHandler().RespondWith(HttpStatusCode.InternalServerError));

        await Assert.ThrowsAsync<HttpRequestException>(() => uploader.UploadAsync("mig-1", path));

        AssertNotLocked(path);
    }

    [Fact]
    public async Task Active_session_retries_pending_uploads_when_connectivity_returns()
    {
        var path = _files.Create("drawing.dwg", 1_000);
        var handler = new ScriptedHttpHandler().FailConnection().OtherwiseRespond(HttpStatusCode.Created);
        var monitor = new NetworkMonitor();
        using var session = new MigrationSession("mig-1", [path], Uploader(handler), monitor);
        await session.RunAsync();
        Assert.Single(session.PendingFiles);

        monitor.Report(online: true);

        await Eventually.TrueAsync(() => session.PendingFiles.Count == 0, TimeSpan.FromSeconds(5), "pending upload retried");
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task Disposed_session_ignores_connectivity_changes()
    {
        var path = _files.Create("drawing.dwg", 1_000);
        var handler = new ScriptedHttpHandler().FailConnection().OtherwiseRespond(HttpStatusCode.Created);
        var monitor = new NetworkMonitor();
        var session = new MigrationSession("mig-1", [path], Uploader(handler), monitor);
        await session.RunAsync();

        session.Dispose();
        monitor.Report(online: true);
        await Task.Delay(200);

        Assert.Equal(1, handler.CallCount);
        Assert.Equal(0, monitor.SubscriberCount);
    }

    [Fact]
    public void Disposed_session_can_be_garbage_collected()
    {
        var monitor = new NetworkMonitor();
        var uploader = Uploader(new ScriptedHttpHandler());

        var collectable = GcAssert.IsCollectable(() =>
        {
            var session = new MigrationSession("mig-1", ["a.pdf"], uploader, monitor);
            session.Dispose();
            return session;
        });

        Assert.True(collectable, "A disposed MigrationSession is still reachable from the app-wide NetworkMonitor.");
        GC.KeepAlive(monitor);
    }

    public void Dispose() => _files.Dispose();

    private static FileUploader Uploader(ScriptedHttpHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://platform.test/") });

    private static void AssertNotLocked(string path)
    {
        // Opening with FileShare.None fails if anyone (including us) still holds a handle.
        using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
        }

        File.Delete(path);
    }
}
