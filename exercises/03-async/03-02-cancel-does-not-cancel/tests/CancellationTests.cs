using System.Net;
using Gym.TestUtilities;
using MigrationKit.Engine;

namespace Ex0302.Engine.Tests;

public sealed class CancellationTests : IDisposable
{
    private static readonly TimeSpan Prompt = TimeSpan.FromSeconds(2);
    private readonly TestFiles _files = new();

    [Fact]
    public async Task Healthy_migration_completes()
    {
        var handler = new ScriptedHttpHandler().OtherwiseRespond(HttpStatusCode.Created);

        var result = await Engine(handler).RunAsync("mig-1", _files.CreateMany(10));

        Assert.Equal(MigrationOutcome.Completed, result.Outcome);
        Assert.Equal(10, result.FilesUploaded);
    }

    [Fact]
    public async Task Cancelling_stops_active_uploads_promptly_and_starts_no_new_ones()
    {
        var handler = new ScriptedHttpHandler().Otherwise(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct); // a slow upload that only ends when cancelled
            throw new InvalidOperationException("unreachable");
        });
        using var cts = new CancellationTokenSource();
        var engine = Engine(handler, o => o.MaxConcurrency = 4);

        var run = engine.RunAsync("mig-1", _files.CreateMany(20), cts.Token);
        await Eventually.TrueAsync(() => handler.CallCount == 4, TimeSpan.FromSeconds(5), "4 uploads in flight");
        cts.Cancel();

        var result = await run.WithTimeout(Prompt, "RunAsync did not return promptly after cancellation.");
        Assert.Equal(MigrationOutcome.Cancelled, result.Outcome);
        await Task.Delay(100);
        Assert.Equal(4, handler.CallCount);
    }

    [Fact]
    public async Task Cancelling_during_a_retry_delay_is_prompt()
    {
        var handler = new ScriptedHttpHandler().OtherwiseRespond(HttpStatusCode.ServiceUnavailable);
        using var cts = new CancellationTokenSource();
        var engine = Engine(handler, o => o.RetryDelay = TimeSpan.FromSeconds(30));

        var run = engine.RunAsync("mig-1", _files.CreateMany(1), cts.Token);
        await Eventually.TrueAsync(() => handler.CallCount == 1, TimeSpan.FromSeconds(5), "first attempt made");
        await Task.Delay(50);
        cts.Cancel();

        var result = await run.WithTimeout(Prompt, "RunAsync kept waiting out the retry delay after cancellation.");
        Assert.Equal(MigrationOutcome.Cancelled, result.Outcome);
    }

    [Fact]
    public async Task A_single_timed_out_attempt_is_retried()
    {
        var handler = new ScriptedHttpHandler().Hang().OtherwiseRespond(HttpStatusCode.Created);
        var engine = Engine(handler, o => o.RequestTimeout = TimeSpan.FromMilliseconds(200));

        var result = await engine.RunAsync("mig-1", _files.CreateMany(1)).WithTimeout();

        Assert.Equal(MigrationOutcome.Completed, result.Outcome);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task Timeouts_are_failures_not_cancellations()
    {
        var files = _files.CreateMany(3);
        var slowFile = Path.GetFileName(files[1]);
        var handler = new ScriptedHttpHandler()
            .Hang(when: r => r.RequestUri!.Query.Contains(slowFile), times: int.MaxValue)
            .OtherwiseRespond(HttpStatusCode.Created);
        var engine = Engine(handler, o =>
        {
            o.RequestTimeout = TimeSpan.FromMilliseconds(200);
            o.MaxAttempts = 2;
        });

        var result = await engine.RunAsync("mig-1", files).WithTimeout();

        Assert.Equal(MigrationOutcome.Failed, result.Outcome);
        Assert.Equal([files[1]], result.FailedFiles);
        Assert.Equal(2, result.FilesUploaded);
        Assert.DoesNotContain("cancel", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose() => _files.Dispose();

    private static MigrationEngine Engine(ScriptedHttpHandler handler, Action<MigrationEngineOptions>? configure = null)
    {
        var options = new MigrationEngineOptions { RetryDelay = TimeSpan.FromMilliseconds(10) };
        configure?.Invoke(options);
        return new MigrationEngine(new HttpClient(handler) { BaseAddress = new Uri("https://platform.test/") }, options);
    }
}
