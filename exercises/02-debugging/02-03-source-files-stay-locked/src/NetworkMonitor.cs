namespace MigrationKit.Transfer;

public interface INetworkMonitor
{
    /// <summary>Raised with <c>true</c> when connectivity is restored and <c>false</c> when it is lost.</summary>
    event EventHandler<bool>? ConnectivityChanged;
}

/// <summary>One instance for the lifetime of the desktop application.</summary>
public sealed class NetworkMonitor : INetworkMonitor
{
    public event EventHandler<bool>? ConnectivityChanged;

    public int SubscriberCount => ConnectivityChanged?.GetInvocationList().Length ?? 0;

    public void Report(bool online) => ConnectivityChanged?.Invoke(this, online);
}
