using System.Net;

namespace MigrationKit.Api;

// ------------------------------------------------------------------
// Error contract (agreed in API review; do not change).
//
//   * Every failure caused by the platform or the network is a MigrationApiException
//     (or a subclass). Callers never have to catch HttpRequestException or JsonException.
//   * StatusCode is the HTTP status, or null when no response was received.
//   * CorrelationId is the X-Correlation-ID sent with the failing request, so support can
//     find the platform's log entry.
//   * Cancellation requested by the caller is NOT wrapped: it surfaces as OperationCanceledException.
// ------------------------------------------------------------------

public class MigrationApiException : Exception
{
    public MigrationApiException(string message, HttpStatusCode? statusCode, string? correlationId, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        CorrelationId = correlationId;
    }

    public HttpStatusCode? StatusCode { get; }

    public string? CorrelationId { get; }
}

/// <summary>The migration does not exist (404).</summary>
public sealed class MigrationNotFoundException(string message, string? correlationId, Exception? innerException = null)
    : MigrationApiException(message, HttpStatusCode.NotFound, correlationId, innerException);

/// <summary>The platform rejected the request as invalid (400). <see cref="Errors"/> is keyed by field name.</summary>
public sealed class MigrationValidationException(
    string message,
    IReadOnlyDictionary<string, string[]> errors,
    string? correlationId,
    Exception? innerException = null)
    : MigrationApiException(message, HttpStatusCode.BadRequest, correlationId, innerException)
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}

/// <summary>The client could not authenticate with its credentials.</summary>
public sealed class MigrationAuthenticationException(string message, HttpStatusCode? statusCode, string? correlationId, Exception? innerException = null)
    : MigrationApiException(message, statusCode, correlationId, innerException);
