namespace MigrationKit.Provisioning;

/// <summary>What the setup wizard shows after the provisioning step.</summary>
public sealed record ProvisioningStepResult(bool Succeeded, string? DestinationId, string? UserMessage, bool CanRetry)
{
    public static ProvisioningStepResult Success(string destinationId) => new(true, destinationId, null, false);

    public static ProvisioningStepResult Failed(string message, bool canRetry) => new(false, null, message, canRetry);
}

public sealed class ProvisioningStep
{
    private readonly ProvisioningClient _client;

    public ProvisioningStep(HttpClient platformHttpClient)
    {
        _client = new ProvisioningClient(platformHttpClient);
    }

    public async Task<ProvisioningStepResult> RunAsync(string customerId, string region, CancellationToken cancellationToken = default)
    {
        var destinationId = await _client.ProvisionAsync(customerId, region, cancellationToken);

        if (destinationId == null)
        {
            return ProvisioningStepResult.Failed("Provisioning failed. Please contact support.", canRetry: false);
        }

        if (destinationId == "RETRY")
        {
            return ProvisioningStepResult.Failed("The service is busy. Please try again in a moment.", canRetry: true);
        }

        return ProvisioningStepResult.Success(destinationId);
    }
}
