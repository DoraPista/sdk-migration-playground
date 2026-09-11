# 06-02 Secrets in the Support Logs – Interviewer Notes

**Type:** security debugging · **Time:** 20 min · **Solution code:** `code/src/`

## The leaks

| # | Where | How it leaks |
|---|---|---|
| 1 | `TokenClient` | `LogInformation("... {Credentials}", _credentials)`: a **record's generated `ToString()`** prints `ClientSecret`. Structured loggers also capture the object |
| 2 | `TokenClient` | Logs the token endpoint's **response body** at Debug, and the body *is* the access token |
| 3 | `TokenClient` | Exception message includes the first 6 characters of the secret and the raw body. "Partial" secrets are still secrets (and shrink brute force) |
| 4 | `HttpLoggingHandler` | `request.Headers.ToString()` includes `Authorization: Bearer ...` |
| 5 | `HttpLoggingHandler` / `BlobUploader` | Full URLs logged. SAS URLs carry `sig=`, a **bearer credential** valid until `se=` |
| 6 | `BlobUploader` | Exception message with the SAS URL: hosts log exceptions |

## Hints

1. "Search the code for every place something is logged or put into an exception message. What's in each value?"
2. "What does a record's `ToString()` print? What's in `request.Headers.ToString()`? What's in a SAS URL?"
3. "Log identifiers, not credentials. Strip query strings. Allow-list headers. Never log token endpoint bodies."

## Intended solution

- `SdkCredentials.ToString()` override (or a class with `[DebuggerDisplay]` redaction; or stop using a record).
- Log `ClientId`, status and OAuth `error` code, never bodies.
- `Redaction.SafeUrl` (scheme + host + path) and `SafeHeaders` (redact `Authorization`, cookies and so on) used everywhere.
- Exception messages built from safe values only.

### Alternatives / defence in depth

- `Microsoft.Extensions.Compliance.Redaction` + data classification attributes (`[PrivateData]`) with `LoggerMessage` source generators. The modern .NET way; senior answer.
- A redacting `ILoggerProvider` wrapper as a last line of defence (regex on `sig=`, `Bearer`, and known secret values). Good as a *safety net*, weak as the primary control.
- Not holding the secret in the SDK at all: accept an `ITokenCredential`-like abstraction so the host manages secrets (DPAPI, Credential Manager, Entra ID interactive).
- Tests like these in CI ("canary secrets" that must never appear in captured output).

## Common mistakes

- Only lowering the log level. Verbose logging is exactly what support enables.
- Masking with `***` but leaving `sig=` intact in exception messages.
- Deny-listing only `Authorization` (misses `Proxy-Authorization`, cookies, custom headers).
- Removing all logging (fails "logs remain useful").
- Forgetting that `LogError(exception, ...)` writes `exception.ToString()`, including inner messages.

## Follow-ups

- "Support now has 50 tickets with live secrets. What do you do?" (Rotate every exposed secret and revoke tokens; SAS URLs expire but can be revoked via stored access policies or key rotation; purge the ticket attachments; assess the breach.)
- "How do you prevent regressions?" (Analyzer or grep rules; canary tests; `LoggerMessage` with classified parameters; code-review checklist.)
- "Is the client ID a secret?" (No, but it is personal/tenant-identifying data: fine for support, not for public telemetry.)

## Scoring notes

| Level | Indicators |
|---|---|
| Weak | Finds the header or the obvious secret only; misses record `ToString()`; lowers log levels |
| Solid mid-level | Finds all six; central redaction helper; exceptions cleaned; logs still useful |
| Strong | Explains SAS as bearer credentials; allow-list over deny-list; structured logging capture of objects |
| Senior | Incident response (rotation, revocation), classification/redaction frameworks, not handling secrets in the SDK at all |
