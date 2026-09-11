using System.Net;
using Gym.TestUtilities;
using MigrationKit.Client;

namespace Ex0204.Client.Tests;

public sealed class UiThreadTests : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);
    private readonly UiThread _ui = new();

    [Fact]
    public async Task Awaiting_ConnectAsync_on_the_ui_thread_resumes_on_the_ui_thread()
    {
        var client = CreateClient(RealisticServer());

        var resumedOnUiThread = await _ui.RunAsync(async () =>
        {
            await client.ConnectAsync();
            return _ui.IsCurrent;
        }).WithTimeout(Patience);

        Assert.True(resumedOnUiThread);
        Assert.True(client.IsConnected);
    }

    [Fact]
    public void Connect_called_from_the_ui_thread_completes()
    {
        var client = CreateClient(RealisticServer());

        var finished = _ui.TryRun(client.Connect, Patience, out var error);

        Assert.True(finished, "Connect() never returned: the UI thread is deadlocked.");
        Assert.Null(error);
        Assert.True(client.IsConnected);
    }

    [Fact]
    public void CheckHealth_called_from_the_ui_thread_completes()
    {
        var client = CreateClient(RealisticServer());
        var healthy = false;

        var finished = _ui.TryRun(() => healthy = client.CheckHealth(), Patience, out var error);

        Assert.True(finished, "CheckHealth() never returned: the UI thread is deadlocked.");
        Assert.Null(error);
        Assert.True(healthy);
    }

    [Fact]
    public void Connect_failure_surfaces_as_HttpRequestException()
    {
        var client = CreateClient(new ScriptedHttpHandler().FailConnection());

        Assert.Throws<HttpRequestException>(client.Connect);
    }

    public void Dispose() => _ui.Dispose();

    private static MigrationClient CreateClient(ScriptedHttpHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://platform.test/") }, "gym-client", "gym-secret-3f9a1c");

    /// <summary>Responds asynchronously, like a real network does.</summary>
    private static ScriptedHttpHandler RealisticServer() => new ScriptedHttpHandler().Otherwise(async (request, ct) =>
    {
        await Task.Delay(10, ct);
        return request.IsPath("auth/token")
            ? Responses.Json(HttpStatusCode.OK, new Dictionary<string, object> { ["access_token"] = "tok_123", ["token_type"] = "Bearer", ["expires_in"] = 3600 })
            : Responses.Json(HttpStatusCode.OK, new { status = "Healthy" });
    });
}
