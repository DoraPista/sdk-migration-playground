namespace MigrationKit.Hosting;

/// <summary>Resolves a customer's cloud destination (a slow call to a directory service).</summary>
public interface IDestinationDirectory
{
    Task<string> GetDestinationAsync(string customerId, CancellationToken cancellationToken);
}

/// <summary>The platform's file upload endpoint.</summary>
public interface IFileSink
{
    Task UploadAsync(string destinationId, string customerId, string file, CancellationToken cancellationToken);
}

/// <summary>Who we are migrating for right now.</summary>
public sealed class CustomerSession
{
    public string? CustomerId { get; private set; }

    public void SignIn(string customerId) => CustomerId = customerId;
}

/// <summary>Knows where the signed-in customer's files go.</summary>
public sealed class UploadTarget
{
    private readonly CustomerSession _session;
    private readonly IDestinationDirectory _directory;
    private string? _destinationId;

    public UploadTarget(CustomerSession session, IDestinationDirectory directory)
    {
        _session = session;
        _directory = directory;
    }

    public async Task<string> GetDestinationIdAsync(CancellationToken cancellationToken)
    {
        var customerId = _session.CustomerId ?? throw new InvalidOperationException("No customer is signed in.");
        return _destinationId ??= await _directory.GetDestinationAsync(customerId, cancellationToken);
    }
}

public sealed class MigrationJob
{
    private readonly CustomerSession _session;
    private readonly UploadTarget _target;
    private readonly IFileSink _sink;

    public MigrationJob(CustomerSession session, UploadTarget target, IFileSink sink)
    {
        _session = session;
        _target = target;
        _sink = sink;
    }

    public async Task RunAsync(string customerId, IReadOnlyList<string> files, CancellationToken cancellationToken = default)
    {
        _session.SignIn(customerId);
        var destinationId = await _target.GetDestinationIdAsync(cancellationToken);

        foreach (var file in files)
        {
            await _sink.UploadAsync(destinationId, _session.CustomerId!, file, cancellationToken);
        }
    }
}
