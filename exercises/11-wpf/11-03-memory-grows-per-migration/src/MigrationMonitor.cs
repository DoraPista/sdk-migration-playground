namespace MigrationKit.Wpf;

public sealed class MigrationProgressEventArgs(string migrationId, double percent) : EventArgs
{
    public string MigrationId { get; } = migrationId;

    public double Percent { get; } = percent;
}

/// <summary>Created once at start-up; lives as long as the application does.</summary>
public interface IMigrationMonitor
{
    event EventHandler<MigrationProgressEventArgs>? ProgressChanged;

    /// <summary>How many handlers are attached (diagnostics).</summary>
    int SubscriberCount { get; }
}

public sealed class MigrationMonitor : IMigrationMonitor
{
    public event EventHandler<MigrationProgressEventArgs>? ProgressChanged;

    public int SubscriberCount => ProgressChanged?.GetInvocationList().Length ?? 0;

    public void Report(string migrationId, double percent) => ProgressChanged?.Invoke(this, new MigrationProgressEventArgs(migrationId, percent));
}
