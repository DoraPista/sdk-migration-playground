namespace Gym.MockServer;

/// <summary>
/// Out-of-band control API so the standalone server (and the demo apps) can be driven
/// without writing C#. Tests normally use <see cref="MockServer.Faults"/> directly.
/// </summary>
internal static class ControlEndpoints
{
    public sealed record FailureModeRequest(FailureMode Mode, string? Route, int? Times);

    public sealed record FaultRequest(
        string Route,
        FaultKind Kind,
        int? StatusCode,
        int? Times,
        int? RetryAfterSeconds,
        int? DelayMilliseconds,
        int? Percentage,
        string? Body);

    public static void Map(WebApplication app)
    {
        var control = app.MapGroup("/_control");

        control.MapPost("/reset", (MockServerState state, FaultInjector faults) =>
        {
            state.Reset();
            faults.Clear();
            return Results.NoContent();
        });

        control.MapPost("/failure-mode", (FailureModeRequest request, FaultInjector faults) =>
        {
            faults.Simulate(request.Mode, request.Route ?? FaultInjector.AnyRoute, request.Times ?? 1);
            return Results.NoContent();
        });

        control.MapPost("/faults", (FaultRequest request, FaultInjector faults) =>
        {
            var fault = new Fault(request.Kind)
            {
                StatusCode = request.StatusCode ?? 500,
                Body = request.Body,
                Delay = TimeSpan.FromMilliseconds(request.DelayMilliseconds ?? 0),
                Percentage = request.Percentage ?? 50,
                Headers = request.RetryAfterSeconds is { } seconds
                    ? new Dictionary<string, string> { ["Retry-After"] = seconds.ToString() }
                    : null,
            };
            faults.Add(request.Route, fault, request.Times ?? 1);
            return Results.NoContent();
        });

        control.MapPost("/expire-tokens", (MockServerState state) =>
        {
            state.RevokeAllTokens();
            return Results.NoContent();
        });

        control.MapPost("/revoke-credentials", (MockServerState state) =>
        {
            state.CredentialsRevoked = true;
            state.RevokeAllTokens();
            return Results.NoContent();
        });

        control.MapGet("/requests", (MockServerState state) => Results.Ok(state.Requests.ToArray()));

        control.MapGet("/files", (MockServerState state) =>
            Results.Ok(state.Migrations.Values.SelectMany(m => m.Files.Select(f => new { migrationId = m.Id, file = f.ToDto() }))));
    }
}
