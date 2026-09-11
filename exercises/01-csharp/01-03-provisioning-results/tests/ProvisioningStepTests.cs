using System.Net;
using Gym.TestUtilities;
using MigrationKit.Provisioning;

namespace Ex0103.Provisioning.Tests;

public sealed class ProvisioningStepTests
{
    private readonly ScriptedHttpHandler _platform = new();

    [Fact]
    public async Task New_customer_gets_a_destination()
    {
        _platform.RespondJson(HttpStatusCode.Created, new { destinationId = "dst-00042", customerId = "CUST-1001", region = "westeurope" });

        var result = await RunAsync();

        Assert.True(result.Succeeded);
        Assert.Equal("dst-00042", result.DestinationId);
    }

    [Fact]
    public async Task Already_provisioned_customer_continues_with_the_existing_destination()
    {
        _platform.RespondJson(HttpStatusCode.Conflict, new
        {
            type = "https://gym.local/problems/already-provisioned",
            title = "Destination already provisioned",
            status = 409,
            destinationId = "dst-00007",
        });

        var result = await RunAsync();

        Assert.True(result.Succeeded);
        Assert.Equal("dst-00007", result.DestinationId);
    }

    [Fact]
    public async Task Network_failure_is_retryable_and_never_yields_a_destination()
    {
        _platform.FailConnection();

        var result = await RunAsync();

        Assert.False(result.Succeeded);
        Assert.Null(result.DestinationId);
        Assert.True(result.CanRetry);
    }

    [Fact]
    public async Task Throttling_is_retryable()
    {
        _platform.RespondTooManyRequests(TimeSpan.FromSeconds(5));

        var result = await RunAsync();

        Assert.False(result.Succeeded);
        Assert.True(result.CanRetry);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Server_errors_are_reported_as_retryable_instead_of_crashing(HttpStatusCode status)
    {
        _platform.RespondWith(status);

        var result = await RunAsync();

        Assert.False(result.Succeeded);
        Assert.True(result.CanRetry);
    }

    [Fact]
    public async Task Validation_problem_is_not_retryable_and_explains_what_is_wrong()
    {
        _platform.RespondJson(HttpStatusCode.BadRequest, new
        {
            type = "https://gym.local/problems/400",
            title = "Validation failed",
            status = 400,
            errors = new Dictionary<string, string[]> { ["region"] = ["Region 'mars-north' is not supported."] },
        });

        var result = await RunAsync();

        Assert.False(result.Succeeded);
        Assert.False(result.CanRetry);
        Assert.Contains("mars-north", result.UserMessage);
    }

    [Fact]
    public async Task User_cancellation_is_not_turned_into_a_result()
    {
        _platform.Hang();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => RunAsync(cts.Token));
    }

    private Task<ProvisioningStepResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var step = new ProvisioningStep(new HttpClient(_platform) { BaseAddress = new Uri("https://platform.test/") });
        return step.RunAsync("CUST-1001", "westeurope", cancellationToken);
    }
}
