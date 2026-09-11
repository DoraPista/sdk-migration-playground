# 05-01 The Migration API Client – Interviewer Notes

**Type:** implementation (HTTP integration) · **Time:** 35 min · **Solution code:** `code/src/MigrationApiClient.cs`
**Spec requirement:** HTTP integration tested against the local mock server

## What the starter code gets wrong

| Area | Problem |
|---|---|
| Auth | No token at all, so every call is 401 |
| JSON | `JsonSerializer.Deserialize<Migration>(json)` uses **case-sensitive PascalCase** defaults; the API is camelCase, so every property is default. Enum values are strings on the wire and need `JsonStringEnumConverter` |
| Errors | `EnsureSuccessStatusCode()` → raw `HttpRequestException`, violating the error contract; `GetStringAsync` throws before you can read the problem body |
| Correlation | No `X-Correlation-ID`, so support can't find the log entry |
| URLs | `$"migrations/{migrationId}"` without escaping. IDs with `/`, `?`, `#` or `..` change the request |
| Disposal | Responses not disposed |
| Missing | `GetStatusAsync` not implemented |

## Hints

1. "What does the server say when you call it right now?" (Run a test and look at the 401.)
2. "How will you avoid requesting a token per call, and what happens when two calls need a token at the same time?"
3. "Put everything that's common (token, headers, send, error mapping, JSON) in one private `SendAsync<T>`; map status codes to the exception types in `Errors.cs`."

## Intended solution (outline)

- One private pipeline: token → request with `Authorization` + fresh `X-Correlation-ID` → send → map errors → deserialize.
- Token cache with expiry (**refresh early**) + `SemaphoreSlim` so concurrent callers don't stampede (see 06-01/06-03 for depth).
- Error mapping reads `application/problem+json` (title/detail/errors) and falls back gracefully if the body isn't JSON (proxies return HTML).
- `HttpRequestException` → `MigrationApiException(statusCode: null)`; an `HttpClient.Timeout` (OCE while the caller's token is **not** cancelled) → `MigrationApiException`; caller cancellation is not wrapped.
- `JsonException` → `MigrationApiException` with the inner exception.
- `ConfigureAwait(false)` throughout (library code; see 02-04).

### Alternatives / design discussion

- **`DelegatingHandler`s** for auth and correlation (`AuthenticationHandler`, `CorrelationHandler`) composed into the `HttpClient` pipeline.
  Very reasonable, and it keeps `MigrationApiClient` thin. But the constraint says the host supplies the `HttpClient`, so how would the SDK insert handlers?
  (Via an `IHttpClientBuilder` extension `AddMigrationKit()` in hosts that use `IHttpClientFactory`, or by accepting an `HttpMessageHandler`.) Great senior-level discussion.
- `IHttpClientFactory` inside the SDK: not available to .NET Framework hosts without extra packages, and not the SDK's decision to make.
- Generated clients (NSwag/Kiota/Refit) from an OpenAPI document: valid for large APIs; the error contract still needs hand-written mapping.
- Correlation ID per *logical operation* (shared across retries) vs per *attempt*. Both are defensible; retries in a later exercise make this concrete.

## Common mistakes

- Setting `_http.DefaultRequestHeaders.Authorization`: mutates a shared, host-owned client; not thread-safe.
- `PropertyNameCaseInsensitive = true` but forgetting the enum converter (or vice versa).
- Mapping 404 on the token endpoint or a 401 on an API call wrongly: a 401 from the **API** means the token expired or was revoked, which is different from bad credentials (06-01 handles refresh).
- Wrapping cancellation in `MigrationApiException`.
- Reading the problem body **after** `EnsureSuccessStatusCode` (it threw already).
- Returning `null!` for an empty body.

## Follow-ups

- "The platform starts returning `429` with `Retry-After`. Where does retry go in this design?" (08-01)
- "How would you test this without the mock server?" (A `ScriptedHttpHandler`, but keep one integration test against a contract-enforcing fake.)
- "Which of these exceptions would you expect callers to catch, and which should crash?"
- "How does the SDK expose logging?" (`ILogger` via options or `ILoggerFactory` in the constructor; never log secrets: 06-02.)

## Scoring notes

| Level | Indicators |
|---|---|
| Weak | Adds a token header by hand per method; copy-pastes error handling; ignores the error contract; casing bugs remain |
| Solid mid-level | One pipeline; cached token; correct mapping to documented exceptions; correlation ID; JSON options right; tests pass |
| Strong | Refresh-early and stampede protection; timeout vs cancellation; non-JSON error bodies; URL escaping; `ConfigureAwait(false)` |
| Senior | Discusses handler composition vs host-owned `HttpClient`, versioning of the error contract, correlation per operation vs attempt, and telemetry |
