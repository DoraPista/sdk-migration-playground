using MigrationKit.Simulation;

namespace Ex0803.RetryStorm.Tests;

public sealed class SimulationTests
{
    [Fact]
    public void Simulation_is_deterministic()
    {
        var first = FleetSimulation.Run(new ClientRetryPolicy());
        var second = FleetSimulation.Run(new ClientRetryPolicy());

        Assert.Equal(first.Seconds, second.Seconds);
    }

    [Fact]
    public void Healthy_fleet_stays_within_capacity()
    {
        var result = FleetSimulation.Run(new ClientRetryPolicy(), new SimulationSettings
        {
            OutageStart = TimeSpan.FromSeconds(1_000),
            OutageEnd = TimeSpan.FromSeconds(1_001),
            Duration = TimeSpan.FromSeconds(60),
        });

        Assert.True(result.PeakOfferedPerSecond <= 200, $"Peak {result.PeakOfferedPerSecond} req/s without any outage.");
        Assert.Equal(0, result.FilesFailed);
    }
}
