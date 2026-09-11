using System.Net;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace Gym.MockServer;

/// <summary>
/// In-process host for the mock Migration Platform API.
/// <code>
/// await using var server = await MockServer.StartAsync();
/// server.Faults.Simulate(FailureMode.FailFirstTwoAttempts, "POST /migrations/{id}/files");
/// </code>
/// </summary>
public sealed class MockServer : IAsyncDisposable
{
    private readonly WebApplication _app;

    private MockServer(WebApplication app, Uri baseAddress)
    {
        _app = app;
        BaseAddress = baseAddress;
    }

    public Uri BaseAddress { get; }

    public MockServerState State => _app.Services.GetRequiredService<MockServerState>();

    public FaultInjector Faults => _app.Services.GetRequiredService<FaultInjector>();

    public MockServerOptions Options => _app.Services.GetRequiredService<MockServerOptions>();

    public static Task<MockServer> StartAsync(CancellationToken cancellationToken = default) =>
        StartAsync(new MockServerOptions(), cancellationToken);

    public static async Task<MockServer> StartAsync(MockServerOptions options, CancellationToken cancellationToken = default)
    {
        var app = Build(options);
        await app.StartAsync(cancellationToken);

        var address = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!
            .Addresses.First();

        return new MockServer(app, new Uri(address.EndsWith('/') ? address : address + "/"));
    }

    /// <summary>An HttpClient pointed at the server with no authentication.</summary>
    public HttpClient CreateClient() => new() { BaseAddress = BaseAddress };

    /// <summary>Returns a valid bearer token (handy for tests that are not about authentication).</summary>
    public string IssueToken() => State.IssueToken(Options.TokenLifetime);

    public void Reset()
    {
        State.Reset();
        Faults.Clear();
    }

    public async ValueTask DisposeAsync()
    {
        _app.Services.GetRequiredService<ServerLifetime>().Stop();
        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    internal static WebApplication Build(MockServerOptions options)
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(MockServer).Assembly.GetName().Name,
        });

        builder.Logging.ClearProviders();
        if (options.ConsoleLogging)
        {
            builder.Logging.AddSimpleConsole(o => o.SingleLine = true);
        }

        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            kestrel.Listen(IPAddress.Loopback, options.Port);
            kestrel.Limits.MaxRequestBodySize = null;
        });

        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton(new MockServerState(options.TimeProvider));
        builder.Services.AddSingleton<FaultInjector>();
        builder.Services.AddSingleton<ServerLifetime>();
        builder.Services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(2));

        var app = builder.Build();
        MockServerPipeline.Configure(app);
        return app;
    }
}

/// <summary>Cancelled when the server is disposed, so hanging requests do not delay shutdown.</summary>
internal sealed class ServerLifetime
{
    private readonly CancellationTokenSource _cts = new();

    public CancellationToken Stopping => _cts.Token;

    public void Stop() => _cts.Cancel();
}
