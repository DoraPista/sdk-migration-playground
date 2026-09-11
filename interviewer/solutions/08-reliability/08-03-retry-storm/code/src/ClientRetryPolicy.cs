namespace MigrationKit.Simulation;

/// <summary>
/// Improved client behaviour:
///   1. ONE retrying layer (the uploader). The HTTP handler and the workflow no longer retry on their own,
///      so attempts add up instead of multiplying (27 → 6).
///   2. Exponential backoff with full jitter: clients that failed at the same moment come back at different moments.
///   3. The platform's Retry-After hint is honoured (plus jitter), so an overloaded platform controls the pace.
///   4. A client that keeps seeing failures slows down further (a simple client-side circuit breaker).
/// </summary>
public sealed class ClientRetryPolicy : IRetryPolicy
{
    private const int MaxAttempts = 6;
    private static readonly TimeSpan BaseDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(60);

    public TimeSpan? NextDelay(int failedAttempts, AttemptResult result, SimulationContext context)
    {
        if (failedAttempts >= MaxAttempts)
        {
            return null;
        }

        if (context.RetryAfterSeconds is { } hint)
        {
            // Spread the herd over [hint, 2*hint] instead of everyone returning at exactly +hint.
            return TimeSpan.FromSeconds(hint + context.Random.NextDouble() * hint);
        }

        var ceiling = Math.Min(MaxDelay.TotalSeconds, BaseDelay.TotalSeconds * Math.Pow(2, failedAttempts));

        // Circuit-breaker flavour: if almost everything this client sent recently failed, assume an outage and wait longer.
        var recent = context.RecentResults;
        if (recent.Count >= 10 && recent.Count(r => r != AttemptResult.Accepted) >= 9)
        {
            ceiling = MaxDelay.TotalSeconds;
        }

        return TimeSpan.FromSeconds(Math.Max(0.2, context.Random.NextDouble() * ceiling));
    }
}
