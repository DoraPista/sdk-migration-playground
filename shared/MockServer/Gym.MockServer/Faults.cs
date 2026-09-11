namespace Gym.MockServer;

public enum FaultKind
{
    /// <summary>Return a fixed status code (and optional body/headers) without running the endpoint.</summary>
    Status,
    /// <summary>Wait, then run the endpoint normally.</summary>
    Delay,
    /// <summary>Never respond (until the client gives up). Simulates a timeout.</summary>
    Hang,
    /// <summary>Abort the TCP connection before doing any work.</summary>
    DropConnection,
    /// <summary>Return 200 with a body that is not valid JSON.</summary>
    MalformedJson,
    /// <summary>Run the endpoint (state IS changed) and then abort the connection: the client never sees the result.</summary>
    ResponseLost,
    /// <summary>Upload endpoints: accept the body up to a percentage of the file, keep what arrived, then abort.</summary>
    DropAtPercentage,
    /// <summary>Upload endpoints: pretend the payload was corrupted in transit.</summary>
    ChecksumMismatch,
    /// <summary>Upload endpoints: read the body at a limited rate.</summary>
    SlowTransfer,
    /// <summary>Revoke every issued token, then continue (the request will get 401).</summary>
    ExpireTokens,
    /// <summary>Forget in-flight upload sessions (as a restart would) and return 503.</summary>
    Restart,
}

public sealed record Fault(FaultKind Kind)
{
    public int StatusCode { get; init; } = 500;
    public string? Body { get; init; }
    public IReadOnlyDictionary<string, string>? Headers { get; init; }
    public TimeSpan Delay { get; init; }
    public int Percentage { get; init; } = 50;
    public int BytesPerSecond { get; init; } = 64 * 1024;

    public static Fault Status(int statusCode, string? body = null) => new(FaultKind.Status) { StatusCode = statusCode, Body = body };

    public static Fault TooManyRequests(TimeSpan? retryAfter = null) => new(FaultKind.Status)
    {
        StatusCode = 429,
        Headers = retryAfter is null
            ? null
            : new Dictionary<string, string> { ["Retry-After"] = ((int)Math.Ceiling(retryAfter.Value.TotalSeconds)).ToString() },
    };

    public static Fault Delayed(TimeSpan delay) => new(FaultKind.Delay) { Delay = delay };
    public static Fault Hang() => new(FaultKind.Hang);
    public static Fault DropConnection() => new(FaultKind.DropConnection);
    public static Fault MalformedJson() => new(FaultKind.MalformedJson);
    public static Fault ResponseLost() => new(FaultKind.ResponseLost);
    public static Fault DropAtPercentage(int percentage) => new(FaultKind.DropAtPercentage) { Percentage = percentage };
    public static Fault ChecksumMismatch() => new(FaultKind.ChecksumMismatch);
    public static Fault Slow(int bytesPerSecond) => new(FaultKind.SlowTransfer) { BytesPerSecond = bytesPerSecond };
    public static Fault ExpireTokens() => new(FaultKind.ExpireTokens);
    public static Fault Restart() => new(FaultKind.Restart);

    internal bool IsHandledByEndpoint =>
        Kind is FaultKind.DropAtPercentage or FaultKind.ChecksumMismatch or FaultKind.SlowTransfer;
}

/// <summary>Named presets so exercises and tests can say what they mean.</summary>
public enum FailureMode
{
    None,
    FailFirstTwoAttempts,
    FailAtPercentage,
    Timeout,
    ChecksumMismatch,
    AuthenticationExpired,
    RateLimited,
    ResponseLost,
    ConnectionDrop,
    ServerRestart,
    SlowTransfer,
}

/// <summary>
/// Deterministic fault script. Rules are matched against a route key such as
/// "POST /migrations/{id}/files" (or "*" for any route) and consumed in order.
/// </summary>
public sealed class FaultInjector
{
    public const string AnyRoute = "*";

    private readonly List<FaultRule> _rules = new();
    private readonly object _gate = new();

    public void Add(string route, Fault fault, int times = 1)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(times, 1);
        lock (_gate)
        {
            _rules.Add(new FaultRule(route.Trim(), fault, times));
        }
    }

    public void Always(string route, Fault fault) => Add(route, fault, int.MaxValue);

    public void Clear()
    {
        lock (_gate)
        {
            _rules.Clear();
        }
    }

    public void Simulate(FailureMode mode, string route = AnyRoute, int times = 1)
    {
        switch (mode)
        {
            case FailureMode.None: Clear(); break;
            case FailureMode.FailFirstTwoAttempts: Add(route, Fault.Status(503), 2); break;
            case FailureMode.FailAtPercentage: Add(route, Fault.DropAtPercentage(60), times); break;
            case FailureMode.Timeout: Add(route, Fault.Hang(), times); break;
            case FailureMode.ChecksumMismatch: Add(route, Fault.ChecksumMismatch(), times); break;
            case FailureMode.AuthenticationExpired: Add(route, Fault.ExpireTokens(), times); break;
            case FailureMode.RateLimited: Add(route, Fault.TooManyRequests(TimeSpan.FromSeconds(2)), times); break;
            case FailureMode.ResponseLost: Add(route, Fault.ResponseLost(), times); break;
            case FailureMode.ConnectionDrop: Add(route, Fault.DropConnection(), times); break;
            case FailureMode.ServerRestart: Add(route, Fault.Restart(), times); break;
            case FailureMode.SlowTransfer: Add(route, Fault.Slow(256 * 1024), times); break;
            default: throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
        }
    }

    /// <summary>Takes the first rule matching the route (consuming one use of it).</summary>
    public Fault? TryTake(string routeKey)
    {
        lock (_gate)
        {
            foreach (var rule in _rules)
            {
                if (rule.Remaining <= 0 || !rule.Matches(routeKey))
                {
                    continue;
                }

                if (rule.Remaining != int.MaxValue)
                {
                    rule.Remaining--;
                }

                return rule.Fault;
            }

            return null;
        }
    }

    private sealed class FaultRule(string route, Fault fault, int remaining)
    {
        public Fault Fault { get; } = fault;
        public int Remaining { get; set; } = remaining;

        public bool Matches(string routeKey) =>
            route == AnyRoute || string.Equals(route, routeKey, StringComparison.OrdinalIgnoreCase);
    }
}
