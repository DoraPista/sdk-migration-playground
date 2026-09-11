using System.Net.Http.Headers;
using System.Net.Http.Json;
using Gym.MockServer;
using Gym.TestUtilities;
using MigrationKit.Transfer;

namespace Ex0702.Resumable.Tests;

/// <summary>Runs against the local mock platform, which really drops connections part-way through a body.</summary>
public sealed class ResumableUploaderTests : IClassFixture<MockServerFixture>, IDisposable
{
    private const string PutRoute = "PUT /uploads/{uploadId}";
    private const long OneMegabyte = 1024 * 1024;
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(60);

    private readonly MockServerFixture _server;
    private readonly TestFiles _files = new();
    private readonly CountingHandler _traffic = new() { InnerHandler = new SocketsHttpHandler() };
    private readonly HttpClient _http;

    public ResumableUploaderTests(MockServerFixture server)
    {
        _server = server;
        _server.Reset();
        _http = new HttpClient(_traffic) { BaseAddress = server.BaseAddress };
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", server.Server.IssueToken());
    }

    [Fact]
    public async Task Upload_completes_on_a_good_connection()
    {
        var path = _files.Create("scan-001.e57", 5 * OneMegabyte);

        var stored = await Uploader().UploadAsync(await CreateMigrationAsync(), path).WithTimeout(Patience);

        Assert.Equal(TestFiles.Sha256Hex(path), stored.Sha256);
    }

    [Fact]
    public async Task Upload_resumes_after_repeated_connection_drops()
    {
        var path = _files.Create("scan-002.e57", 20 * OneMegabyte);
        var migrationId = await CreateMigrationAsync();
        _server.Faults.Add(PutRoute, Fault.DropAtPercentage(60), times: 4);

        var stored = await Uploader().UploadAsync(migrationId, path).WithTimeout(Patience);

        Assert.Equal(TestFiles.Sha256Hex(path), stored.Sha256);
        Assert.Single(_server.State.Migrations[migrationId].Files);
    }

    [Fact]
    public async Task Bytes_the_server_already_has_are_not_sent_again()
    {
        var path = _files.Create("scan-003.e57", 20 * OneMegabyte);
        var migrationId = await CreateMigrationAsync();
        _server.Faults.Add(PutRoute, Fault.DropAtPercentage(60), times: 4);

        await Uploader().UploadAsync(migrationId, path).WithTimeout(Patience);

        Assert.Single(_server.State.Uploads);
        Assert.True(_traffic.PutBytes < 2 * 20 * OneMegabyte, $"{_traffic.PutBytes / OneMegabyte} MB sent for a 20 MB file.");
    }

    [Fact]
    public async Task Lost_upload_session_is_replaced()
    {
        var path = _files.Create("scan-004.e57", 4 * OneMegabyte);
        var migrationId = await CreateMigrationAsync();
        _server.Faults.Add(PutRoute, Fault.Restart());

        var stored = await Uploader().UploadAsync(migrationId, path).WithTimeout(Patience);

        Assert.Equal(TestFiles.Sha256Hex(path), stored.Sha256);
    }

    [Fact]
    public async Task Uploader_gives_up_when_attempts_stop_making_progress()
    {
        var path = _files.Create("scan-005.e57", 2 * OneMegabyte);
        var migrationId = await CreateMigrationAsync();
        _server.Faults.Always(PutRoute, Fault.DropConnection());

        await Assert.ThrowsAnyAsync<Exception>(() => Uploader().UploadAsync(migrationId, path).WithTimeout(Patience));

        Assert.InRange(_server.State.RequestsTo(PutRoute).Count, 1, 3);
    }

    [Fact]
    public async Task Empty_file_is_uploaded()
    {
        var path = _files.Create("documents/empty-notes.txt", 0);

        var stored = await Uploader().UploadAsync(await CreateMigrationAsync(), path).WithTimeout(Patience);

        Assert.Equal(0, stored.Size);
    }

    public void Dispose() => _files.Dispose();

    private ResumableUploader Uploader() =>
        new(_http, new ResumableUploadOptions { MaxAttemptsWithoutProgress = 3, RetryDelay = TimeSpan.FromMilliseconds(10) });

    private async Task<string> CreateMigrationAsync()
    {
        var response = await _http.PostAsJsonAsync("migrations", new { customerId = "CUST-1004", destinationId = "dst-00014", name = "Trust scans" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Created>())!.Id;
    }

    private sealed record Created(string Id);

    /// <summary>Counts the body bytes the client tried to send in PUT requests.</summary>
    private sealed class CountingHandler : DelegatingHandler
    {
        private long _putBytes;

        public long PutBytes => Interlocked.Read(ref _putBytes);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Put)
            {
                Interlocked.Add(ref _putBytes, request.Content?.Headers.ContentLength ?? 0);
            }

            return base.SendAsync(request, cancellationToken);
        }
    }
}
