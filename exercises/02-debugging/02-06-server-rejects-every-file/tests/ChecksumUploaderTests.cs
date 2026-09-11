using System.Net.Http.Headers;
using System.Net.Http.Json;
using Gym.MockServer;
using Gym.TestUtilities;
using MigrationKit.Upload;

namespace Ex0206.Upload.Tests;

public sealed class ChecksumUploaderTests : IClassFixture<MockServerFixture>
{
    private readonly MockServerFixture _server;
    private readonly HttpClient _http;

    public ChecksumUploaderTests(MockServerFixture server)
    {
        _server = server;
        _server.Reset();
        _http = new HttpClient { BaseAddress = server.BaseAddress };
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", server.Server.IssueToken());
    }

    [Fact]
    public async Task Empty_file_is_accepted()
    {
        var migrationId = await CreateMigrationAsync();

        await new ChecksumUploader(_http).UploadAsync(migrationId, MockDataPaths.SampleFile("documents/empty-notes.txt"));

        Assert.Single(_server.State.Migrations[migrationId].Files);
    }

    [Theory]
    [InlineData("documents/project-brief.txt")]
    [InlineData("survey/survey-data.csv")]
    [InlineData("images/facade-render.png")]
    public async Task Files_arrive_intact(string sample)
    {
        var path = MockDataPaths.SampleFile(sample);
        var migrationId = await CreateMigrationAsync();

        await new ChecksumUploader(_http).UploadAsync(migrationId, path);

        var stored = Assert.Single(_server.State.Migrations[migrationId].Files);
        Assert.Equal(new FileInfo(path).Length, stored.Size);
        Assert.Equal(TestFiles.Sha256Hex(path), stored.Sha256);
    }

    [Fact]
    public async Task Upload_recovers_from_a_transient_server_error()
    {
        var migrationId = await CreateMigrationAsync();
        _server.Faults.Add("POST /migrations/{id}/files", Fault.Status(503));

        await new ChecksumUploader(_http).UploadAsync(migrationId, MockDataPaths.SampleFile("documents/project-brief.txt"));

        Assert.Single(_server.State.Migrations[migrationId].Files);
        Assert.Equal(2, _server.State.RequestsTo("POST /migrations/{id}/files").Count);
    }

    private async Task<string> CreateMigrationAsync()
    {
        var response = await _http.PostAsJsonAsync("migrations", new { customerId = "CUST-1001", destinationId = "dst-1", name = "pilot" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Created>())!.Id;
    }

    private sealed record Created(string Id);
}
