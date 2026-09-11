using Gym.MockServer;

// Standalone mode:
//   dotnet run --project shared/MockServer/Gym.MockServer -- --port 5080
//   dotnet run --project shared/MockServer/Gym.MockServer -- --failure-mode FailFirstTwoAttempts --route "POST /migrations/{id}/files"

var options = new MockServerOptions { Port = 5080, ConsoleLogging = true };
FailureMode? mode = null;
var route = FaultInjector.AnyRoute;
var times = 1;

for (var i = 0; i < args.Length; i++)
{
    string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"Missing value for {args[i]}");

    switch (args[i])
    {
        case "--port": options.Port = int.Parse(Next()); break;
        case "--token-lifetime": options.TokenLifetime = TimeSpan.FromSeconds(int.Parse(Next())); break;
        case "--failure-mode": mode = Enum.Parse<FailureMode>(Next(), ignoreCase: true); break;
        case "--route": route = Next(); break;
        case "--times": times = int.Parse(Next()); break;
        case "--quiet": options.ConsoleLogging = false; break;
        case "--help":
            Console.WriteLine("Options: --port N  --token-lifetime SECONDS  --failure-mode MODE  --route \"POST /path\"  --times N  --quiet");
            Console.WriteLine("Modes:   " + string.Join(", ", Enum.GetNames<FailureMode>()));
            return;
        default:
            Console.Error.WriteLine($"Unknown argument '{args[i]}'. Use --help.");
            return;
    }
}

await using var server = await MockServer.StartAsync(options);
if (mode is { } m)
{
    server.Faults.Simulate(m, route, times);
    Console.WriteLine($"Failure mode {m} active on '{route}' ({times}x).");
}

Console.WriteLine($"Mock Migration Platform API listening on {server.BaseAddress}");
Console.WriteLine($"Client credentials: client_id={options.ClientId} client_secret={options.ClientSecret}");
Console.WriteLine("Control API: POST /_control/reset, /_control/failure-mode, /_control/faults, /_control/expire-tokens; GET /_control/requests");
Console.WriteLine("Press Ctrl+C to stop.");

var stop = new TaskCompletionSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    stop.TrySetResult();
};
await stop.Task;
