using System.Net;
using System.Text.Json;
using Gym.MockServer;
using Gym.TestUtilities;
using MigrationKit.Api;

namespace Ex0501.Api.Tests;

/// <summary>Integration tests against the local mock platform (started in-process).</summary>
public sealed class MigrationApiClientTests : IClassFixture<MockServerFixture>
{
    private readonly MockServerFixture _server;

    public MigrationApiClientTests(MockServerFixture server)
    {
        _server = server;
        _server.Reset();
    }

    [Fact]
    public async Task Created_migration_can_be_read_back()
    {
        var client = CreateClient();

        var created = await client.CreateMigrationAsync(new CreateMigrationRequest("CUST-1001", "dst-00011", "Harbour Bridge archive"));
        var read = await client.GetMigrationAsync(created.Id);

        Assert.Equal(created, read);
        Assert.Equal("Harbour Bridge archive", read.Name);
        Assert.Equal(MigrationState.Created, read.State);
    }

    [Fact]
    public async Task Status_of_a_migration_can_be_queried()
    {
        var client = CreateClient();
        var created = await client.CreateMigrationAsync(new CreateMigrationRequest("CUST-1001", "dst-00011", "Riverside"));

        var status = await client.GetStatusAsync(created.Id);

        Assert.Equal(created.Id, status.Id);
        Assert.Equal(MigrationState.Created, status.State);
        Assert.Equal(0, status.FilesReceived);
    }

    [Fact]
    public async Task Access_token_is_obtained_once_and_reused()
    {
        var client = CreateClient();

        var created = await client.CreateMigrationAsync(new CreateMigrationRequest("CUST-1001", "dst-00011", "x"));
        await client.GetMigrationAsync(created.Id);
        await client.GetStatusAsync(created.Id);

        Assert.Equal(1, _server.State.TokenRequestCount);
    }

    [Fact]
    public async Task Invalid_credentials_are_reported_as_an_authentication_failure()
    {
        var client = CreateClient(secret: "not-the-secret");

        await Assert.ThrowsAsync<MigrationAuthenticationException>(() => client.GetMigrationAsync("mig-00001"));
    }

    [Fact]
    public async Task Unknown_migration_is_reported_as_not_found()
    {
        var client = CreateClient();

        var error = await Assert.ThrowsAsync<MigrationNotFoundException>(() => client.GetMigrationAsync("mig-99999"));

        Assert.Equal(HttpStatusCode.NotFound, error.StatusCode);
    }

    [Fact]
    public async Task Validation_errors_are_reported_per_field()
    {
        var client = CreateClient();

        var error = await Assert.ThrowsAsync<MigrationValidationException>(() =>
            client.CreateMigrationAsync(new CreateMigrationRequest("CUST-1001", "", "No destination")));

        Assert.Contains("destinationId", error.Errors.Keys);
    }

    [Fact]
    public async Task Server_errors_carry_the_status_and_the_correlation_id_of_the_failed_request()
    {
        var client = CreateClient();
        var created = await client.CreateMigrationAsync(new CreateMigrationRequest("CUST-1001", "dst-00011", "x"));
        _server.Faults.Add("GET /migrations/{id}", Fault.Status(503));

        var error = await Assert.ThrowsAnyAsync<MigrationApiException>(() => client.GetMigrationAsync(created.Id));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, error.StatusCode);
        var failedRequest = _server.State.RequestsTo("GET /migrations/{id}").Single(r => r.StatusCode == 503);
        Assert.False(string.IsNullOrEmpty(failedRequest.CorrelationId));
        Assert.Equal(failedRequest.CorrelationId, error.CorrelationId);
    }

    [Fact]
    public async Task Every_call_is_traceable_on_the_server()
    {
        var client = CreateClient();

        var created = await client.CreateMigrationAsync(new CreateMigrationRequest("CUST-1001", "dst-00011", "x"));
        await client.GetMigrationAsync(created.Id);
        await client.GetStatusAsync(created.Id);

        var apiCalls = _server.State.Requests.Where(r => r.Path.StartsWith("/migrations")).ToArray();
        Assert.Equal(3, apiCalls.Length);
        Assert.All(apiCalls, r => Assert.False(string.IsNullOrEmpty(r.CorrelationId)));
        Assert.Equal(3, apiCalls.Select(r => r.CorrelationId).Distinct().Count());
    }

    [Fact]
    public async Task Malformed_response_is_reported_as_an_api_error()
    {
        var client = CreateClient();
        var created = await client.CreateMigrationAsync(new CreateMigrationRequest("CUST-1001", "dst-00011", "x"));
        _server.Faults.Add("GET /migrations/{id}", Fault.MalformedJson());

        var error = await Assert.ThrowsAnyAsync<MigrationApiException>(() => client.GetMigrationAsync(created.Id));

        Assert.IsAssignableFrom<JsonException>(error.InnerException);
    }

    [Fact]
    public async Task Cancellation_surfaces_as_cancellation()
    {
        var client = CreateClient();
        _server.Faults.Add("GET /migrations/{id}", Fault.Hang());
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetMigrationAsync("mig-00001", cts.Token));
    }

    private MigrationApiClient CreateClient(string? secret = null) => new(
        new HttpClient { BaseAddress = _server.BaseAddress },
        new MigrationApiOptions { ClientId = _server.Options.ClientId, ClientSecret = secret ?? _server.Options.ClientSecret });
}
