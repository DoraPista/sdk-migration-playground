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
    private MigrationHandle? _current;

    public MigrationController(IMigrationPlatform platform, IMigrationExecutor executor)
    {
        _platform = platform;
        _executor = executor;
    }

    public bool IsRunning => _current is { Completion.IsCompleted: false };

    /// <summary>
    /// Starts a migration for the customer, or returns the one that is already running.
    /// </summary>
    public async Task<MigrationHandle> StartAsync(string customerId, CancellationToken cancellationToken = default)
    {
        if (IsRunning)
        {
            return _current!;
        }

        var migrationId = await _platform.CreateMigrationAsync(customerId, cancellationToken);
        var completion = _executor.RunAsync(migrationId, cancellationToken);

        _current = new MigrationHandle(migrationId, completion);
        return _current;
    }
}
