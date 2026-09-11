using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace MigrationKit.Diagnostics;

/// <summary>Logs every HTTP call the SDK makes (enabled by "Verbose logging" in the desktop app).</summary>
public sealed class HttpLoggingHandler : DelegatingHandler
{
    private readonly ILogger<HttpLoggingHandler> _logger;

    public HttpLoggingHandler(ILogger<HttpLoggingHandler> logger)
    {
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _logger.LogDebug("HTTP {Method} {Uri} Headers: {Headers}", request.Method, request.RequestUri, request.Headers.ToString());

        var stopwatch = Stopwatch.StartNew();
        var response = await base.SendAsync(request, cancellationToken);

        _logger.LogDebug("HTTP {Status} from {Uri} in {Elapsed} ms", (int)response.StatusCode, request.RequestUri, stopwatch.ElapsedMilliseconds);
        return response;
    }
}
