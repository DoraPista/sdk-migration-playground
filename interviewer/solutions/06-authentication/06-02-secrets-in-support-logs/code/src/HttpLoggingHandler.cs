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
        var url = Redaction.SafeUrl(request.RequestUri);
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("HTTP {Method} {Url} Headers: {Headers}", request.Method, url, Redaction.SafeHeaders(request.Headers));
        }

        var stopwatch = Stopwatch.StartNew();
        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        _logger.LogDebug("HTTP {Status} from {Method} {Url} in {Elapsed} ms", (int)response.StatusCode, request.Method, url, stopwatch.ElapsedMilliseconds);
        return response;
    }
}
