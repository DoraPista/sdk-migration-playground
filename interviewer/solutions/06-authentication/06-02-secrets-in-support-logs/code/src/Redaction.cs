using System.Net.Http.Headers;

namespace MigrationKit.Diagnostics;

/// <summary>One place that decides what is safe to write to a log.</summary>
internal static class Redaction
{
    private static readonly HashSet<string> SensitiveHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization", "Proxy-Authorization", "Cookie", "Set-Cookie", "x-ms-copy-source-authorization",
    };

    /// <summary>Scheme, host and path only. Query strings carry SAS signatures and other tokens.</summary>
    public static string SafeUrl(Uri? uri)
    {
        if (uri is null)
        {
            return "(none)";
        }

        return uri.IsAbsoluteUri
            ? uri.GetLeftPart(UriPartial.Path) + (string.IsNullOrEmpty(uri.Query) ? string.Empty : "?[redacted]")
            : uri.OriginalString.Split('?')[0];
    }

    /// <summary>Header names, with values only for headers known to be harmless (allow-list, not deny-list).</summary>
    public static string SafeHeaders(HttpHeaders headers) =>
        string.Join(", ", headers.Select(h => SensitiveHeaders.Contains(h.Key) ? $"{h.Key}: [redacted]" : $"{h.Key}: {string.Join(",", h.Value)}"));
}
