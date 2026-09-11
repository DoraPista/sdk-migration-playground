namespace Gym.MockServer;

public sealed class MockServerOptions
{
    /// <summary>0 = pick a free port (recommended for tests).</summary>
    public int Port { get; set; }

    public string ClientId { get; set; } = "gym-client";

    public string ClientSecret { get; set; } = "gym-secret-3f9a1c";

    public TimeSpan TokenLifetime { get; set; } = TimeSpan.FromHours(1);

    /// <summary>Clock used for token expiry. Tests can pass a FakeTimeProvider.</summary>
    public TimeProvider TimeProvider { get; set; } = TimeProvider.System;

    /// <summary>Files up to this size keep their bytes in memory so tests can compare content.</summary>
    public long RetainContentUpToBytes { get; set; } = 1024 * 1024;

    public bool ConsoleLogging { get; set; }
}
