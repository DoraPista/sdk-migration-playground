using MigrationKit.Simulation;

namespace Ex0803.RetryStorm.Tests;

/// <summary>What the improved policy must achieve. Run these against the candidate's policy too.</summary>
public sealed class ImprovedPolicyTests
{
    [Fact]
    public void Fleet_does_not_pile_onto_a_struggling_platform()
    {
        var result = FleetSimulation.Run(new ClientRetryPolicy());

        // The original policy peaks at ~810 req/s against a platform that can serve 200.
        Assert.True(result.PeakOfferedPerSecond <= 400, $"Peak offered load was {result.PeakOfferedPerSecond} req/s.");
    }

    [Fact]
    public void Platform_recovers_as_soon_as_the_outage_ends()
    {
        var result = FleetSimulation.Run(new ClientRetryPolicy());

        // The original policy never recovers within the simulated 7 minutes.
        Assert.True(result.RecoverySeconds <= 10, $"Recovery took {result.RecoverySeconds} s after the outage ended.");
    }

    [Fact]
    public void Retries_do_not_multiply_the_work()
    {
        var result = FleetSimulation.Run(new ClientRetryPolicy());

        // The original policy sends 5.65 requests per uploaded file; a healthy fleet sends ~1.
        Assert.True(result.RequestsPerUploadedFile <= 2.0, $"{result.RequestsPerUploadedFile:0.00} requests per uploaded file.");
        Assert.True(result.FilesFailed < result.FilesUploaded / 20, $"{result.FilesFailed} files failed.");
    }
}
