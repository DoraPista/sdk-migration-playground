using System.Net;
using Gym.TestUtilities;
using Microsoft.Extensions.Time.Testing;

namespace Gym.Shared.Tests;

public sealed class TestUtilitiesTests
{
    [Fact]
    public async Task Scripted_handler_plays_responses_in_order_then_falls_back()
    {
        var handler = new ScriptedHttpHandler()
            .RespondSequence(HttpStatusCode.ServiceUnavailable, HttpStatusCode.BadGateway)
            .OtherwiseRespond(HttpStatusCode.OK);
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.test/") };

        var codes = new[]
        {
            (await client.GetAsync("a")).StatusCode,
            (await client.GetAsync("a")).StatusCode,
            (await client.GetAsync("a")).StatusCode,
        };

        Assert.Equal([HttpStatusCode.ServiceUnavailable, HttpStatusCode.BadGateway, HttpStatusCode.OK], codes);
        Assert.Equal(3, handler.CallCount);
    }

    [Fact]
    public async Task Scripted_handler_hang_is_released_by_cancellation()
    {
        var handler = new ScriptedHttpHandler().Hang();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.test/") };
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetAsync("slow", cts.Token));
    }

    [Fact]
    public void Ui_thread_detects_sync_over_async_deadlock()
    {
        using var ui = new UiThread();

        var finished = ui.TryRun(() => BlockOnAsync().GetAwaiter().GetResult(), TimeSpan.FromMilliseconds(500), out _);

        Assert.False(finished);

        static async Task BlockOnAsync()
        {
            await Task.Delay(10); // resumes on the captured single-threaded context -> deadlock
        }
    }

    [Fact]
    public async Task Fake_time_can_be_driven_until_a_task_completes()
    {
        var time = new FakeTimeProvider();
        var waiting = Task.Delay(TimeSpan.FromMinutes(5), time);

        var advanced = await time.AdvanceUntilCompleteAsync(waiting, TimeSpan.FromSeconds(30));

        Assert.True(advanced >= TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void Test_files_are_deterministic()
    {
        using var a = new TestFiles();
        using var b = new TestFiles();

        Assert.Equal(TestFiles.Sha256Hex(a.Create("x.bin", 100_000, seed: 3)), TestFiles.Sha256Hex(b.Create("x.bin", 100_000, seed: 3)));
    }

    [Fact]
    public void Gc_assert_detects_retained_objects()
    {
        var keepAlive = new List<object>();

        Assert.True(GcAssert.IsCollectable(() => new object()));
        Assert.False(GcAssert.IsCollectable(() =>
        {
            var o = new object();
            keepAlive.Add(o);
            return o;
        }));
    }
}
