namespace MigrationKit.Control;

public interface IMigrationPlatform
{
    /// <summary>Creates a migration on the platform and returns its id. Slow on bad connections.</summary>
    Task<string> CreateMigrationAsync(string customerId, CancellationToken cancellationToken);
}

public interface IMigrationExecutor
{
    /// <summary>Uploads everything for the migration. Runs for minutes to hours.</summary>
    Task RunAsync(string migrationId, CancellationToken cancellationToken);
}

/// <summary>A started migration: its id and a task that completes when the migration finishes.</summary>
public sealed record MigrationHandle(string MigrationId, Task Completion);

public sealed class MigrationController
{
    private readonly IMigrationPlatform _platform;
    private readonly IMigrationExecutor _executor;
    private readonly object _gate = new();

    // The in-flight START operation (creating the migration) — not just the finished handle.
    // Sharing the task is what makes the second click wait for the first click's result.
    private Task<MigrationHandle>? _current;

    public MigrationController(IMigrationPlatform platform, IMigrationExecutor executor)
    {
        _platform = platform;
        _executor = executor;
    }

    public bool IsRunning
    {
        get
        {
            lock (_gate)
            {
                return _current is not null && !IsFinished(_current);
            }
        }
    }

    /// <summary>
    /// Starts a migration for the customer, or returns the one that is being started or is running.
    /// </summary>
    public Task<MigrationHandle> StartAsync(string customerId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_current is not null && !IsFinished(_current))
            {
                return _current;
            }

            // Check-and-set happens atomically under the lock; the slow work happens outside it.
            // Note: the first caller's token governs the shared start. A second click is a "join".
            _current = StartCoreAsync(customerId, cancellationToken);
            return _current;
        }
    }

    private async Task<MigrationHandle> StartCoreAsync(string customerId, CancellationToken cancellationToken)
    {
        // Yield so the platform call never runs while the lock above is held.
        await Task.Yield();
        var migrationId = await _platform.CreateMigrationAsync(customerId, cancellationToken);
        var completion = _executor.RunAsync(migrationId, cancellationToken);
        return new MigrationHandle(migrationId, completion);
    }

    /// <summary>A failed or cancelled start, or a finished migration, allows a new start.</summary>
    private static bool IsFinished(Task<MigrationHandle> start) =>
        start.IsFaulted
        || start.IsCanceled
        || (start.IsCompletedSuccessfully && start.Result.Completion.IsCompleted);
}
