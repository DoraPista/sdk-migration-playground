using System.Text;
using Gym.TestUtilities;
using Microsoft.Extensions.Time.Testing;
using MigrationKit.Resilient;

namespace Ex1302.Resilient.Tests;

public sealed class ResilientUploaderTests
{
    private static readonly UploadRequest Request = new("documents/tender.pdf", Encoding.UTF8.GetBytes("tender"));

    private readonly FakeTimeProvider _time = new();

    [Fact]
    public async Task A_healthy_upload_succeeds_at_the_first_attempt()
    {
        var api = NewApi();

        var receipt = await Upload(api).WithTimeout();

        Assert.Equal("file-00001", receipt.FileId);
        Assert.Equal(1, receipt.Attempts);
        Assert.Empty(api.Aborted);
    }

    [Fact]
    public async Task Retries_until_the_platform_accepts_the_file()
    {
        var api = NewApi(failFirst: 3);

        var upload = Upload(api);
        await _time.AdvanceUntilCompleteAsync(upload, TimeSpan.FromMilliseconds(250));

        Assert.Equal(4, (await upload).Attempts);
        Assert.Equal(4, api.Sends);
        Assert.Empty(api.Aborted);
    }

    [Fact]
    public async Task Every_retry_sends_the_same_content_to_the_same_session()
    {
        var api = NewApi(failFirst: 2);

        var upload = Upload(api);
        await _time.AdvanceUntilCompleteAsync(upload, TimeSpan.FromMilliseconds(250));
        await upload;

        Assert.Equal(["upl-00001", "upl-00001", "upl-00001"], api.SessionsUsed);
        Assert.All(api.ContentSent, sent => Assert.Equal(Request.Content, sent));
        Assert.Equal(1, api.SessionsStarted); // one session, not one per attempt
    }

    [Fact]
    public async Task Backoff_is_one_two_and_four_seconds()
    {
        var api = NewApi(failFirst: 3);

        var upload = Upload(api);
        await _time.AdvanceUntilCompleteAsync(upload, TimeSpan.FromMilliseconds(250));
        await upload;

        // Attempts at t = 0s, 1s, 1+2s and 1+2+4s.
        Assert.Equal([0, 1, 3, 7], api.SendTimes.Select(t => (int)t.TotalSeconds));
    }

    [Fact]
    public async Task It_gives_up_after_four_attempts_and_reports_the_failure()
    {
        var api = NewApi(failFirst: 99);

        var upload = Upload(api);
        var failure = await Record.ExceptionAsync(() => _time.AdvanceUntilCompleteAsync(upload, TimeSpan.FromMilliseconds(250)));

        Assert.IsType<HttpRequestException>(failure);
        Assert.Equal(4, api.Sends);
    }

    [Fact]
    public async Task Giving_up_releases_the_session()
    {
        var api = NewApi(failFirst: 99);

        var upload = Upload(api);
        await Record.ExceptionAsync(() => _time.AdvanceUntilCompleteAsync(upload, TimeSpan.FromMilliseconds(250)));

        Assert.Equal(["upl-00001"], api.Aborted);
    }

    [Fact]
    public async Task Cancelling_stops_the_upload()
    {
        using var cts = new CancellationTokenSource();
        var api = NewApi();
        api.BlockNextSend();

        var upload = Upload(api, cts.Token);
        await api.SendReached.WaitAsync().WithTimeout();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => upload).WithTimeout();
    }

    /// <summary>This is the support ticket: 4,000 abandoned sessions from cancelled migrations.</summary>
    [Fact]
    public async Task Cancelling_releases_the_session_on_the_platform()
    {
        using var cts = new CancellationTokenSource();
        var api = NewApi();
        api.BlockNextSend();

        var upload = Upload(api, cts.Token);
        await api.SendReached.WaitAsync().WithTimeout();
        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => upload).WithTimeout();

        Assert.Equal(["upl-00001"], api.Aborted);
    }

    [Fact]
    public async Task Cancelling_during_the_backoff_also_releases_the_session()
    {
        using var cts = new CancellationTokenSource();
        var api = NewApi(failFirst: 99);

        var upload = Upload(api, cts.Token);
        await api.SendReached.WaitAsync().WithTimeout(); // the first attempt has failed; we are in the delay
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => upload).WithTimeout();
        Assert.Equal(["upl-00001"], api.Aborted);
        Assert.Equal(1, api.Sends); // the clock never moved, so no retry happened
    }

    [Fact]
    public async Task A_failing_abort_does_not_hide_the_original_cancellation()
    {
        using var cts = new CancellationTokenSource();
        var api = NewApi();
        api.AbortThrows = true;
        api.BlockNextSend();

        var upload = Upload(api, cts.Token);
        await api.SendReached.WaitAsync().WithTimeout();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => upload).WithTimeout();
    }

    private FakeUploadApi NewApi(int failFirst = 0) => new(_time) { FailFirst = failFirst };

    private Task<UploadReceipt> Upload(FakeUploadApi api, CancellationToken cancellationToken = default) =>
        new ResilientUploader(api, _time).UploadAsync("mig-00102", Request, cancellationToken);

    /// <summary>Records what the platform was asked to do, and can block a send so cancellation has something to interrupt.</summary>
    private sealed class FakeUploadApi(TimeProvider time) : IUploadApi
    {
        private readonly DateTimeOffset _start = time.GetUtcNow();
        private AsyncGate? _block;

        public int FailFirst { get; set; }

        public bool AbortThrows { get; set; }

        public int Sends { get; private set; }

        public int SessionsStarted { get; private set; }

        public List<string> SessionsUsed { get; } = [];

        public List<byte[]> ContentSent { get; } = [];

        public List<TimeSpan> SendTimes { get; } = [];

        public List<string> Aborted { get; } = [];

        /// <summary>Opens as soon as the uploader reaches <see cref="SendAsync"/>.</summary>
        public AsyncGate SendReached { get; } = new();

        /// <summary>Makes the next send hang until it is cancelled.</summary>
        public void BlockNextSend() => _block = new AsyncGate();

        public Task<string> StartSessionAsync(string migrationId, UploadRequest request, CancellationToken cancellationToken)
        {
            SessionsStarted++;
            return Task.FromResult("upl-00001");
        }

        public async Task<string> SendAsync(string sessionId, byte[] content, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Sends++;
            SessionsUsed.Add(sessionId);
            ContentSent.Add(content);
            SendTimes.Add(time.GetUtcNow() - _start);
            SendReached.Open();

            if (_block is { } gate)
            {
                await gate.WaitAsync(cancellationToken); // never opened: cancellation is the only way out
            }

            if (Sends <= FailFirst)
            {
                throw new HttpRequestException("The platform is unavailable (503).");
            }

            return "file-00001";
        }

        public Task AbortSessionAsync(string sessionId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested(); // catches an abort sent with the cancelled token
            if (AbortThrows)
            {
                throw new HttpRequestException("Abort failed (500).");
            }

            Aborted.Add(sessionId);
            return Task.CompletedTask;
        }
    }
}
