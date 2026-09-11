# Exercise 06-03 – The Token Cache

Difficulty: Medium
Estimated Time: 25 minutes

## Skills

- token lifetime and caching
- time in tests
- cache keys
- concurrency

## Scenario

The SDK caches access tokens to avoid calling the authorization server for every request. Consultants use
the same desktop app (and the same client registration) to migrate several customers, each in its
own tenant.

Reported problems:

1. A consultant finished Northwind's migration and started Contoso's. The first Contoso uploads went to
   **Northwind's tenant**.
2. Long uploads started just before the token expires fail halfway with `401`. The platform checks the token
   when the request *completes*, not when it starts.
3. The integration test suite is flaky around the changes to and from daylight saving time. Someone added a
   `TimeProvider` to the constructor "for tests", but it didn't help.
4. The authorization server team sees bursts of identical token requests when a migration starts.

## Your Task

Fix `TokenCache`.

## Constraints

- Keep the public API.
- Rule agreed with the platform team: a token handed out by the cache must have **at least 60 seconds** of
  remaining lifetime.

## Acceptance Criteria

- Tokens are reused while they satisfy the rule above, and renewed when they don't.
- Tokens for different tenants or scopes are never mixed up.
- Separate cache instances don't share tokens.
- Concurrent callers needing the same token cause one request to the authorization server.

## How to Run

```bash
dotnet test exercises/06-authentication/06-03-token-cache/tests
```

## When You're Done

All tests pass, and you can explain problem 3.
