using Gym.MockServer;
using Xunit;

namespace Gym.TestUtilities;

/// <summary>
/// xUnit fixture that runs the mock API in-process on a free port.
/// Use as <c>IClassFixture&lt;MockServerFixture&gt;</c> and call <see cref="Reset"/> in the test constructor
/// so every test starts from a clean server.
/// </summary>
public sealed class MockServerFixture : IAsyncLifetime
{
    private MockServer.MockServer? _server;

    public MockServer.MockServer Server => _server ?? throw new InvalidOperationException("Server not started.");

    public Uri BaseAddress => Server.BaseAddress;

    public MockServerOptions Options => Server.Options;

    public FaultInjector Faults => Server.Faults;

    public MockServerState State => Server.State;

    public void Reset() => Server.Reset();

    public async Task InitializeAsync() => _server = await MockServer.MockServer.StartAsync();

    public async Task DisposeAsync()
    {
        if (_server is not null)
        {
            await _server.DisposeAsync();
        }
    }
}
