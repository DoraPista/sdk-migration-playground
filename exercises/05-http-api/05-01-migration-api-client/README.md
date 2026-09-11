# Exercise 05-01 – The Migration API Client

Difficulty: Hard
Estimated Time: 35 minutes

## Skills

- HTTP client design
- authentication (client credentials)
- JSON contracts
- error mapping
- diagnosability (correlation IDs)
- cancellation

## Scenario

The SDK needs a client for the Migration Platform API. The **public surface was agreed in API review**
and must not change. It's in `src/MigrationApiClient.cs` (the methods) and `src/Errors.cs` (the error contract).

A junior developer started the implementation before going on leave. Their notes: *"create works against
the mock server if I paste a token into Postman… the rest I haven't got to."*

Your colleagues in support also asked for one thing: when a customer sends them an error message, they want to
find the matching server log entry. The platform logs the `X-Correlation-ID` request header.

The platform contract is `shared/MockServer/API.md`.

## Your Task

Implement `MigrationApiClient` so that it honours the public contract.

## Constraints

- Don't change the public types or signatures (you may add private/internal members and types).
- Authentication is OAuth2 client credentials against `POST /auth/token`.
- `HttpClient` is supplied by the host application (the host decides proxies, handlers, lifetime).

## Acceptance Criteria

- Migrations can be created and read back, and their status queried.
- The client authenticates by itself and doesn't request a new token for every call.
- Failures surface as the documented exception types (see `Errors.cs`), with the HTTP status and the correlation ID of the failed request.
- A response that isn't valid JSON is reported as an API error, not a raw `JsonException`.
- Cancellation surfaces as cancellation.

## How to Run

```bash
dotnet test exercises/05-http-api/05-01-migration-api-client/tests
```

The tests start the mock server in-process. You can also run it yourself:

```bash
dotnet run --project shared/MockServer/Gym.MockServer
```

## When You're Done

All tests pass, and you can walk through what happens, request by request, on the first call.
