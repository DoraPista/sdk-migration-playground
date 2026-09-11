using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Gym.MockServer;
using Gym.Models.Api;
using Gym.TestUtilities;

namespace Gym.Shared.Tests;

public sealed class MockServerTests : IClassFixture<MockServerFixture>
{
    private readonly MockServerFixture _fixture;
    private readonly HttpClient _http;

    public MockServerTests(MockServerFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
        _http = new HttpClient { BaseAddress = fixture.BaseAddress, Timeout = TimeSpan.FromSeconds(10) };
    }

    [Fact]
    public async Task Health_does_not_require_authentication()
    {
        var response = await _http.GetAsync("health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Token_endpoint_accepts_form_and_rejects_bad_secret()
    {
        var ok = await _http.PostAsync("auth/token", Form(_fixture.Options.ClientSecret));
        var token = await ok.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.Equal("Bearer", token!.TokenType);

        var bad = await _http.PostAsync("auth/token", Form("wrong"));
        Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);
        Assert.Equal(2, _fixture.State.TokenRequestCount);
    }

    [Fact]
    public async Task Protected_endpoints_require_a_valid_token()
    {
        var response = await _http.PostAsJsonAsync("migrations", new CreateMigrationRequest("CUST-1001", "dst-1", "x"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Fail_first_two_attempts_then_succeeds()
    {
        Authorize();
        _fixture.Faults.Simulate(FailureMode.FailFirstTwoAttempts, "POST /migrations");

        var codes = new List<HttpStatusCode>();
        for (var i = 0; i < 3; i++)
        {
            codes.Add((await _http.PostAsJsonAsync("migrations", new CreateMigrationRequest("CUST-1001", "dst-1", "x"))).StatusCode);
        }

        Assert.Equal([HttpStatusCode.ServiceUnavailable, HttpStatusCode.ServiceUnavailable, HttpStatusCode.Created], codes);
    }

    [Fact]
    public async Task Rate_limit_fault_sends_retry_after()
    {
        Authorize();
        _fixture.Faults.Add("POST /provision", Fault.TooManyRequests(TimeSpan.FromSeconds(7)));

        var response = await _http.PostAsJsonAsync("provision", new ProvisionRequest("CUST-1001", "westeurope"));

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(7), response.Headers.RetryAfter?.Delta);
    }

    [Fact]
    public async Task Provisioning_twice_returns_conflict_with_existing_destination()
    {
        Authorize();
        var first = await _http.PostAsJsonAsync("provision", new ProvisionRequest("CUST-1001", "westeurope"));
        var second = await _http.PostAsJsonAsync("provision", new ProvisionRequest("CUST-1001", "westeurope"));
        var created = await first.Content.ReadFromJsonAsync<ProvisionResponse>();

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Contains(created!.DestinationId, await second.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Upload_verifies_hash_and_decodes_unicode_names()
    {
        Authorize();
        var migration = await CreateMigrationAsync();
        var content = Encoding.UTF8.GetBytes("Grundriss");

        var response = await UploadAsync(migration.Id, "Übersicht 設計図.txt", content);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var stored = Assert.Single(_fixture.State.Migrations[migration.Id].Files);
        Assert.Equal("Übersicht 設計図.txt", stored.Name);
        Assert.Equal(content, stored.Content);
    }

    [Fact]
    public async Task Upload_with_wrong_hash_is_rejected()
    {
        Authorize();
        var migration = await CreateMigrationAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"migrations/{migration.Id}/files") { Content = new ByteArrayContent([1, 2, 3]) };
        request.Headers.Add(ApiHeaders.FileName, "a.bin");
        request.Headers.Add(ApiHeaders.ContentSha256, new string('a', 64));

        var response = await _http.SendAsync(request);

        Assert.Equal((HttpStatusCode)422, response.StatusCode);
    }

    [Fact]
    public async Task Response_lost_still_stores_the_file()
    {
        Authorize();
        var migration = await CreateMigrationAsync();
        _fixture.Faults.Simulate(FailureMode.ResponseLost, "POST /migrations/{id}/files");

        await Assert.ThrowsAsync<HttpRequestException>(() => UploadAsync(migration.Id, "a.txt", [1, 2, 3]));

        Assert.Single(_fixture.State.Migrations[migration.Id].Files);
    }

    [Fact]
    public async Task Idempotency_key_prevents_duplicate_files()
    {
        Authorize();
        var migration = await CreateMigrationAsync();

        await UploadAsync(migration.Id, "a.txt", [1, 2, 3], idempotencyKey: "key-1");
        await UploadAsync(migration.Id, "a.txt", [1, 2, 3], idempotencyKey: "key-1");
        await UploadAsync(migration.Id, "a.txt", [1, 2, 3]);

        Assert.Equal(2, _fixture.State.Migrations[migration.Id].Files.Count);
    }

    [Fact]
    public async Task Hanging_endpoint_times_out_on_the_client()
    {
        Authorize();
        _fixture.Faults.Simulate(FailureMode.Timeout, "GET /migrations/{id}");
        using var impatient = new HttpClient { BaseAddress = _fixture.BaseAddress, Timeout = TimeSpan.FromMilliseconds(300) };
        impatient.DefaultRequestHeaders.Authorization = _http.DefaultRequestHeaders.Authorization;

        await Assert.ThrowsAsync<TaskCanceledException>(() => impatient.GetAsync("migrations/mig-1"));
    }

    [Fact]
    public async Task Resumable_upload_keeps_bytes_received_before_a_drop()
    {
        Authorize();
        var migration = await CreateMigrationAsync();
        var data = new byte[200_000];
        new Random(1).NextBytes(data);
        var sha = Convert.ToHexStringLower(SHA256.HashData(data));

        var start = await _http.PostAsJsonAsync($"migrations/{migration.Id}/uploads", new StartUploadRequest("big.bin", data.Length, sha, null));
        var session = (await start.Content.ReadFromJsonAsync<UploadSessionDto>())!;
        _fixture.Faults.Add("PUT /uploads/{uploadId}", Fault.DropAtPercentage(40));

        await Assert.ThrowsAnyAsync<HttpRequestException>(() => PutChunkAsync(session.UploadId, data, 0));
        var afterDrop = (await _http.GetFromJsonAsync<UploadSessionDto>($"uploads/{session.UploadId}"))!;
        Assert.Equal(80_000, afterDrop.Received);

        var resumed = await PutChunkAsync(session.UploadId, data, afterDrop.Received);
        Assert.Equal(HttpStatusCode.OK, resumed.StatusCode);
        var complete = await _http.PostAsync($"uploads/{session.UploadId}/complete", null);
        Assert.Equal(HttpStatusCode.Created, complete.StatusCode);
    }

    [Fact]
    public async Task Correlation_id_is_echoed_and_recorded()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "health");
        request.Headers.Add(ApiHeaders.CorrelationId, "corr-123");

        var response = await _http.SendAsync(request);

        Assert.Equal("corr-123", response.Headers.GetValues(ApiHeaders.CorrelationId).Single());
        Assert.Contains(_fixture.State.Requests, r => r.CorrelationId == "corr-123");
    }

    private void Authorize() =>
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _fixture.Server.IssueToken());

    private FormUrlEncodedContent Form(string secret) => new(new Dictionary<string, string>
    {
        ["grant_type"] = "client_credentials",
        ["client_id"] = _fixture.Options.ClientId,
        ["client_secret"] = secret,
    });

    private async Task<MigrationDto> CreateMigrationAsync()
    {
        var response = await _http.PostAsJsonAsync("migrations", new CreateMigrationRequest("CUST-1001", "dst-1", "test"));
        return (await response.Content.ReadFromJsonAsync<MigrationDto>())!;
    }

    private async Task<HttpResponseMessage> UploadAsync(string migrationId, string name, byte[] content, string? idempotencyKey = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"migrations/{migrationId}/files") { Content = new ByteArrayContent(content) };
        request.Headers.Add(ApiHeaders.FileName, Uri.EscapeDataString(name));
        request.Headers.Add(ApiHeaders.ContentSha256, Convert.ToHexStringLower(SHA256.HashData(content)));
        if (idempotencyKey is not null)
        {
            request.Headers.Add(ApiHeaders.IdempotencyKey, idempotencyKey);
        }

        return await _http.SendAsync(request);
    }

    private async Task<HttpResponseMessage> PutChunkAsync(string uploadId, byte[] data, long offset)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"uploads/{uploadId}")
        {
            Content = new ByteArrayContent(data, (int)offset, (int)(data.Length - offset)),
        };
        request.Content.Headers.ContentRange = new ContentRangeHeaderValue(offset, data.Length - 1, data.Length);
        return await _http.SendAsync(request);
    }
}
