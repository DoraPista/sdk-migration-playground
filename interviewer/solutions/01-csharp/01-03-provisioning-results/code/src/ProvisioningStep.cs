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
        var outcome = await _client.ProvisionAsync(customerId, region, cancellationToken);

        return outcome switch
        {
            ProvisioningOutcome.Provisioned p => ProvisioningStepResult.Success(p.DestinationId),
            ProvisioningOutcome.Rejected r => ProvisioningStepResult.Failed($"The destination could not be set up: {r.Reason}", canRetry: false),
            ProvisioningOutcome.TransientFailure t => ProvisioningStepResult.Failed($"{t.Reason} Please try again in a moment.", canRetry: true),
            _ => throw new InvalidOperationException($"Unhandled provisioning outcome {outcome.GetType().Name}."),
        };
    }
}
