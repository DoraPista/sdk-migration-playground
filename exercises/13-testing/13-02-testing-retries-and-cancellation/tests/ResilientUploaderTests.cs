using System.Text;
using MigrationKit.Resilient;

namespace Ex1302.Resilient.Tests;

public sealed class ResilientUploaderTests
{
    private static readonly UploadRequest Request = new("documents/tender.pdf", Encoding.UTF8.GetBytes("tender"));

    [Fact]
    public async Task A_healthy_upload_succeeds_at_the_first_attempt()
    {
        var api = new FakeUploadApi();

        var receipt = await new ResilientUploader(api).UploadAsync("mig-00102", Request);

        Assert.Equal("file-00001", receipt.FileId);
        Assert.Equal(1, receipt.Attempts);
    }

    [Fact(Skip = "Takes ~15 seconds because of the backoff; someone should make this testable.")]
    public async Task Retries_until_the_platform_accepts_the_file()
    {
        var api = new FakeUploadApi { FailFirst = 3 };

        var receipt = await new ResilientUploader(api).UploadAsync("mig-00102", Request);

        Assert.Equal(4, receipt.Attempts);
    }

    /// <summary>A minimal fake; extend it as you need.</summary>
    private sealed class FakeUploadApi : IUploadApi
    {
        private int _sends;

        public int FailFirst { get; set; }

        public Task<string> StartSessionAsync(string migrationId, UploadRequest request, CancellationToken cancellationToken) =>
            Task.FromResult("upl-00001");

        public Task<string> SendAsync(string sessionId, byte[] content, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (++_sends <= FailFirst)
            {
                throw new HttpRequestException("The platform is unavailable (503).");
            }

            return Task.FromResult("file-00001");
        }

        public Task AbortSessionAsync(string sessionId, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
