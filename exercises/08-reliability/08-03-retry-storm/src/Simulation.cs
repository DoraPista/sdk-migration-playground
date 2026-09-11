namespace MigrationKit.Simulation;

public enum AttemptResult
{
    /// <summary>The platform accepted the request.</summary>
    Accepted,

    /// <summary>The platform is down (503).</summary>
    Unavailable,

    /// <summary>The platform is up but over capacity (503 with Retry-After).</summary>
    Overloaded,
}

/// <summary>What a retry policy can see when deciding what to do.</summary>
public sealed class SimulationContext(Random random)
{
    public Random Random { get; } = random;

    /// <summary>Retry-After (seconds) sent by the platform with an Overloaded response, else null.</summary>
    public int? RetryAfterSeconds { get; internal set; }

    /// <summary>Recent outcomes as seen by THIS client (most recent last); a client can base decisions on them.</summary>
    public IReadOnlyList<AttemptResult> RecentResults => _recent;

    internal readonly List<AttemptResult> _recent = new();

    internal void Record(AttemptResult result)
    {
        _recent.Add(result);
        if (_recent.Count > 20)
        {
            _recent.RemoveAt(0);
        }
    }
}

public interface IRetryPolicy
{
    /// <summary>
    /// Called after an attempt failed. Return the delay before the next attempt, or null to give up on this file.
    /// <paramref name="failedAttempts"/> counts the failed attempts for this file so far (1 after the first failure).
    /// </summary>
    TimeSpan? NextDelay(int failedAttempts, AttemptResult result, SimulationContext context);
}

public sealed record SimulationSettings
{
    public int Clients { get; init; } = 300;

    /// <summary>Each client starts a new file upload this often while things are healthy.</summary>
    public TimeSpan FileInterval { get; init; } = TimeSpan.FromSeconds(2);

    public int CapacityPerSecond { get; init; } = 200;

    public TimeSpan OutageStart { get; init; } = TimeSpan.FromSeconds(60);

    public TimeSpan OutageEnd { get; init; } = TimeSpan.FromSeconds(120);

    public TimeSpan Duration { get; init; } = TimeSpan.FromSeconds(420);

    /// <summary>Retry-After the platform sends when overloaded (null = it doesn't send one).</summary>
    public int? OverloadRetryAfterSeconds { get; init; } = 2;

    public int Seed { get; init; } = 2026;
}

public sealed record SecondStats(int Second, int Offered, int Accepted, int Rejected, int FilesFailed);

public sealed record SimulationResult(
    IReadOnlyList<SecondStats> Seconds,
    int TotalRequests,
    int FilesStarted,
    int FilesUploaded,
    int FilesFailed,
    int PeakOfferedPerSecond,
    double RecoverySeconds)
{
    /// <summary>Requests sent per file that was eventually uploaded.</summary>
    public double RequestsPerUploadedFile => FilesUploaded == 0 ? double.PositiveInfinity : (double)TotalRequests / FilesUploaded;
}

/// <summary>
/// Discrete-time simulation (100 ms ticks) of desktop clients uploading files to a platform with fixed capacity.
/// Deterministic for a given seed and policy.
/// </summary>
public static class FleetSimulation
{
    private const int TicksPerSecond = 10;

    public static SimulationResult Run(IRetryPolicy policy, SimulationSettings? settings = null)
    {
        settings ??= new SimulationSettings();
        var random = new Random(settings.Seed);
        var clients = Enumerable.Range(0, settings.Clients).Select(i => new Client(i, random, settings)).ToArray();
        var capacityPerTick = settings.CapacityPerSecond / TicksPerSecond;
        var totalTicks = (int)(settings.Duration.TotalSeconds * TicksPerSecond);
        var seconds = new List<SecondStats>();
        var totals = new Totals();
        int offered = 0, accepted = 0, rejected = 0, failed = 0;

        for (var tick = 0; tick < totalTicks; tick++)
        {
            var now = TimeSpan.FromSeconds((double)tick / TicksPerSecond);
            var down = now >= settings.OutageStart && now < settings.OutageEnd;

            // Everyone who wants to send in this tick, in random order (the network doesn't queue fairly).
            var senders = clients.Where(c => c.WantsToSend(tick)).OrderBy(_ => random.Next()).ToArray();

            // An overloaded platform spends capacity on requests it ends up rejecting (connections, auth, logging,
            // timeouts), so the harder it is hammered, the less useful work it gets done.
            var overload = Math.Max(0, senders.Length - capacityPerTick);
            var budget = Math.Max(1, capacityPerTick - (int)(overload * 0.3));
            foreach (var client in senders)
            {
                AttemptResult result;
                if (down)
                {
                    result = AttemptResult.Unavailable;
                }
                else if (budget > 0)
                {
                    budget--;
                    result = AttemptResult.Accepted;
                }
                else
                {
                    result = AttemptResult.Overloaded;
                }

                offered++;
                totals.Requests++;
                if (result == AttemptResult.Accepted) accepted++; else rejected++;
                failed += client.Complete(tick, result, policy, totals);
            }

            if ((tick + 1) % TicksPerSecond == 0)
            {
                seconds.Add(new SecondStats(tick / TicksPerSecond, offered, accepted, rejected, failed));
                offered = accepted = rejected = failed = 0;
            }
        }

        var outageEndSecond = (int)settings.OutageEnd.TotalSeconds;
        var recovered = seconds.FirstOrDefault(s => s.Second >= outageEndSecond && s.Rejected <= s.Offered / 100);
        var recovery = recovered is null ? double.PositiveInfinity : recovered.Second - outageEndSecond;

        return new SimulationResult(
            seconds,
            totals.Requests,
            totals.FilesStarted,
            totals.FilesUploaded,
            totals.FilesFailed,
            seconds.Max(s => s.Offered),
            recovery);
    }

    private sealed class Totals
    {
        public int Requests;
        public int FilesStarted;
        public int FilesUploaded;
        public int FilesFailed;
    }

    private sealed class Client
    {
        private readonly SimulationSettings _settings;
        private readonly SimulationContext _context;
        private readonly int _intervalTicks;
        private int _nextNewFileTick;
        private int? _retryTick;
        private int _failedAttempts;

        public Client(int index, Random random, SimulationSettings settings)
        {
            _settings = settings;
            _context = new SimulationContext(new Random(random.Next()));
            _intervalTicks = (int)(settings.FileInterval.TotalSeconds * TicksPerSecond);
            _nextNewFileTick = index % _intervalTicks; // clients start spread out, as they do in real life
        }

        public bool WantsToSend(int tick) => _retryTick is { } retry ? retry <= tick : _nextNewFileTick <= tick;

        /// <summary>Returns 1 if the client gave up on its current file.</summary>
        public int Complete(int tick, AttemptResult result, IRetryPolicy policy, Totals totals)
        {
            if (_retryTick is null)
            {
                totals.FilesStarted++;
            }

            _context.Record(result);
            if (result == AttemptResult.Accepted)
            {
                totals.FilesUploaded++;
                StartNextFile(tick);
                return 0;
            }

            _failedAttempts++;
            _context.RetryAfterSeconds = result == AttemptResult.Overloaded ? _settings.OverloadRetryAfterSeconds : null;
            var delay = policy.NextDelay(_failedAttempts, result, _context);
            if (delay is null)
            {
                totals.FilesFailed++;
                StartNextFile(tick);
                return 1;
            }

            _retryTick = tick + Math.Max(1, (int)Math.Round(delay.Value.TotalSeconds * TicksPerSecond));
            return 0;
        }

        private void StartNextFile(int tick)
        {
            _retryTick = null;
            _failedAttempts = 0;
            _nextNewFileTick = tick + _intervalTicks;
        }
    }
}
