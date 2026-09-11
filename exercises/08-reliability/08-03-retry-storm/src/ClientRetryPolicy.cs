namespace MigrationKit.Simulation;

/// <summary>
/// What one desktop client does after a failed attempt. This summarises the SDK's current behaviour:
///
///   * MigrationWorkflow retries a failed file 3 times,
///   * FileUploader (inside it) retries each upload 3 times,
///   * RetryingHttpHandler (inside that) retries each request 3 times,
///
/// each layer waiting a fixed 1 second, so one logical upload can become 3 × 3 × 3 = 27 requests.
/// </summary>
public sealed class ClientRetryPolicy : IRetryPolicy
{
    private const int Layers = 3;
    private const int AttemptsPerLayer = 3;

    public int MaxAttempts => (int)Math.Pow(AttemptsPerLayer, Layers); // 27

    public TimeSpan? NextDelay(int failedAttempts, AttemptResult result, SimulationContext context)
    {
        if (failedAttempts >= MaxAttempts)
        {
            return null; // give up on this file; the migration moves on to the next one
        }

        // The two inner layers retry straight away; only the outer layer waits (a fixed second, for everyone).
        return failedAttempts % AttemptsPerLayer == 0 ? TimeSpan.FromSeconds(1) : TimeSpan.Zero;
    }
}
