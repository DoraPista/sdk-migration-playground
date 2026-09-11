using Gym.Models.Api;

namespace Gym.MockServer;

internal static class MockServerPipeline
{
    internal const string RouteKeyItem = "gym.route";
    internal const string FaultItem = "gym.fault";
    internal const string AbortedItem = "gym.aborted";

    public static void Configure(WebApplication app)
    {
        app.UseRouting();
        app.Use(RecordAndCorrelateAsync);
        app.Use(InjectFaultsAsync);
        app.Use(AuthenticateAsync);
        MockServerEndpoints.Map(app);
        ControlEndpoints.Map(app);
    }

    internal static void MarkAborted(HttpContext context)
    {
        context.Items[AbortedItem] = true;
        context.Abort();
    }

    private static string RouteKey(HttpContext context)
    {
        var pattern = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? context.Request.Path.Value ?? "/";
        if (!pattern.StartsWith('/'))
        {
            pattern = "/" + pattern;
        }

        return $"{context.Request.Method} {pattern}";
    }

    private static bool IsControl(HttpContext context) =>
        context.Request.Path.StartsWithSegments("/_control");

    private static async Task RecordAndCorrelateAsync(HttpContext context, RequestDelegate next)
    {
        var state = context.RequestServices.GetRequiredService<MockServerState>();
        var routeKey = RouteKey(context);
        context.Items[RouteKeyItem] = routeKey;

        var correlationId = context.Request.Headers[ApiHeaders.CorrelationId].FirstOrDefault();
        if (!string.IsNullOrEmpty(correlationId))
        {
            context.Response.Headers[ApiHeaders.CorrelationId] = correlationId;
        }

        try
        {
            await next(context);
        }
        catch (Exception ex) when (context.Items.ContainsKey(AbortedItem) || context.RequestAborted.IsCancellationRequested)
        {
            // The connection is gone (on purpose or because the client gave up); nothing left to send.
            _ = ex;
        }
        finally
        {
            if (!IsControl(context))
            {
                var aborted = context.Items.ContainsKey(AbortedItem) || context.RequestAborted.IsCancellationRequested;
                state.Requests.Enqueue(new RequestRecord(
                    context.Request.Method,
                    context.Request.Path.Value ?? "/",
                    routeKey,
                    aborted ? 0 : context.Response.StatusCode,
                    aborted,
                    correlationId,
                    context.Request.Headers[ApiHeaders.IdempotencyKey].FirstOrDefault(),
                    context.Request.Headers.Authorization.Count > 0,
                    state.Now));
            }
        }
    }

    private static async Task InjectFaultsAsync(HttpContext context, RequestDelegate next)
    {
        if (IsControl(context))
        {
            await next(context);
            return;
        }

        var faults = context.RequestServices.GetRequiredService<FaultInjector>();
        var fault = faults.TryTake((string)context.Items[RouteKeyItem]!);
        if (fault is null)
        {
            await next(context);
            return;
        }

        if (fault.IsHandledByEndpoint)
        {
            context.Items[FaultItem] = fault;
            await next(context);
            return;
        }

        var lifetime = context.RequestServices.GetRequiredService<ServerLifetime>();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, lifetime.Stopping);

        switch (fault.Kind)
        {
            case FaultKind.Status:
                context.Response.StatusCode = fault.StatusCode;
                if (fault.Headers is not null)
                {
                    foreach (var (name, value) in fault.Headers)
                    {
                        context.Response.Headers[name] = value;
                    }
                }

                context.Response.ContentType = "application/problem+json";
                await context.Response.WriteAsync(
                    fault.Body ?? $$"""{"type":"https://gym.local/problems/injected","title":"Injected failure","status":{{fault.StatusCode}}}""",
                    linked.Token);
                return;

            case FaultKind.Delay:
                await Task.Delay(fault.Delay, linked.Token);
                await next(context);
                return;

            case FaultKind.Hang:
                try
                {
                    await Task.Delay(Timeout.Infinite, linked.Token);
                }
                catch (OperationCanceledException)
                {
                }

                MarkAborted(context);
                return;

            case FaultKind.DropConnection:
                MarkAborted(context);
                return;

            case FaultKind.MalformedJson:
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("""{"id":"mig-00001","state":"Upload""", linked.Token);
                return;

            case FaultKind.ResponseLost:
                // Let the endpoint do its work, but never deliver the response.
                context.Response.Body = new MemoryStream();
                await next(context);
                MarkAborted(context);
                return;

            case FaultKind.ExpireTokens:
                context.RequestServices.GetRequiredService<MockServerState>().RevokeAllTokens();
                await next(context);
                return;

            case FaultKind.Restart:
                context.RequestServices.GetRequiredService<MockServerState>().Uploads.Clear();
                context.Response.StatusCode = 503;
                await context.Response.WriteAsync("""{"title":"Service restarting","status":503}""", linked.Token);
                return;

            default:
                await next(context);
                return;
        }
    }

    private static async Task AuthenticateAsync(HttpContext context, RequestDelegate next)
    {
        var path = context.Request.Path;
        if (path.StartsWithSegments("/health") || path.StartsWithSegments("/auth") || IsControl(context))
        {
            await next(context);
            return;
        }

        var state = context.RequestServices.GetRequiredService<MockServerState>();
        var header = context.Request.Headers.Authorization.FirstOrDefault();
        const string prefix = "Bearer ";
        if (header is not null
            && header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && state.IsTokenValid(header[prefix.Length..].Trim()))
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers.WWWAuthenticate = header is null
            ? "Bearer"
            : "Bearer error=\"invalid_token\", error_description=\"The access token is expired or invalid\"";
        await context.Response.WriteAsJsonAsync(
            new ProblemDto("https://gym.local/problems/unauthorized", "Unauthorized", 401),
            context.RequestAborted);
    }
}
