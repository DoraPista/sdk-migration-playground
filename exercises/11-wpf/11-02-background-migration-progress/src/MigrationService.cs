namespace MigrationKit.Wpf;

public sealed class FileUploadedEventArgs(string name) : EventArgs
{
    public string Name { get; } = name;
}

public sealed class MigrationProgressEventArgs(double percent, long bytesSent) : EventArgs
{
    public double Percent { get; } = percent;

    public long BytesSent { get; } = bytesSent;
}

/// <summary>The SDK's migration service. Events are raised on whatever thread the work runs on.</summary>
public interface IMigrationService
{
    event EventHandler<FileUploadedEventArgs>? FileUploaded;

    event EventHandler<MigrationProgressEventArgs>? ProgressChanged;

    Task RunAsync(CancellationToken cancellationToken = default);
}
