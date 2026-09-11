using System.Collections.Concurrent;
using Gym.TestUtilities;
using Microsoft.Extensions.DependencyInjection;
using MigrationKit.Hosting;

namespace Ex0205.Hosting.Tests;

public sealed class MultiCustomerTests
{
    private static readonly Dictionary<string, string> Destinations = new()
    {
        ["CUST-1001"] = "dst-northwind",
        ["CUST-1002"] = "dst-contoso",
        ["CUST-1003"] = "dst-fabrikam",
    };

    [Fact]
    public async Task Single_job_uploads_to_the_customer_destination()
    {
        var sink = new RecordingSink();
        var services = BuildApp(new Directory(), sink);

        await JobHost.RunJobAsync(services, "CUST-1001", ["a.pdf", "b.pdf"]);

        Assert.All(sink.Uploads, u => Assert.Equal(("dst-northwind", "CUST-1001"), (u.Destination, u.Customer)));
    }

    [Fact]
    public async Task Consecutive_jobs_for_different_customers_use_their_own_destinations()
    {
        var sink = new RecordingSink();
        var services = BuildApp(new Directory(), sink);

        await JobHost.RunJobAsync(services, "CUST-1001", ["northwind-1.pdf"]);
        await JobHost.RunJobAsync(services, "CUST-1002", ["contoso-1.pdf"]);

        var contoso = Assert.Single(sink.Uploads, u => u.File == "contoso-1.pdf");
        Assert.Equal("dst-contoso", contoso.Destination);
        Assert.Equal("CUST-1002", contoso.Customer);
    }

    [Fact]
    public async Task Concurrent_jobs_do_not_mix_up_customers()
    {
        var gate = new AsyncGate();
        var sink = new RecordingSink();
        var services = BuildApp(new Directory(gate), sink);

        // Both jobs sign in and then wait for the (slow) directory lookup at the same time.
        var fabrikam = JobHost.RunJobAsync(services, "CUST-1003", ["fabrikam-1.pdf", "fabrikam-2.pdf"]);
        var contoso = JobHost.RunJobAsync(services, "CUST-1002", ["contoso-1.pdf", "contoso-2.pdf"]);
        await gate.WhenWaitingAsync(1);
        await Task.Delay(100); // let a second lookup arrive too, if the code makes one
        gate.Open();
        await Task.WhenAll(fabrikam, contoso).WithTimeout();

        Assert.All(sink.Uploads.Where(u => u.File.StartsWith("fabrikam")), u => Assert.Equal(("dst-fabrikam", "CUST-1003"), (u.Destination, u.Customer)));
        Assert.All(sink.Uploads.Where(u => u.File.StartsWith("contoso")), u => Assert.Equal(("dst-contoso", "CUST-1002"), (u.Destination, u.Customer)));
        Assert.Equal(4, sink.Uploads.Count);
    }

    /// <summary>Mirrors the desktop app's composition root.</summary>
    private static IServiceProvider BuildApp(IDestinationDirectory directory, IFileSink sink)
    {
        var services = new ServiceCollection();
        services.AddSingleton(directory);
        services.AddSingleton(sink);
        services.AddMigrationKit();
        return services.BuildServiceProvider();
    }

    private sealed class Directory(AsyncGate? gate = null) : IDestinationDirectory
    {
        public async Task<string> GetDestinationAsync(string customerId, CancellationToken cancellationToken)
        {
            if (gate is not null)
            {
                await gate.WaitAsync(cancellationToken);
            }

            await Task.Yield();
            return Destinations[customerId];
        }
    }

    private sealed record Upload(string Destination, string Customer, string File);

    private sealed class RecordingSink : IFileSink
    {
        private readonly ConcurrentQueue<Upload> _uploads = new();

        public IReadOnlyList<Upload> Uploads => _uploads.ToArray();

        public async Task UploadAsync(string destinationId, string customerId, string file, CancellationToken cancellationToken)
        {
            await Task.Yield();
            _uploads.Enqueue(new Upload(destinationId, customerId, file));
        }
    }
}
