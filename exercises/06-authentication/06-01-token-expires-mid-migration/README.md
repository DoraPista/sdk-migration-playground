# Exercise 06-01 – The Token Expires in the Middle of a Migration

Difficulty: Hard
Estimated Time: 40 minutes

## Skills

- authentication flows (OAuth2 client credentials)
- `DelegatingHandler`
- concurrency (single-flight refresh)
- failure modes that loop forever

## Scenario

Access tokens issued by the platform are valid for one hour. Large migrations run for many hours with
8 uploads in parallel, so tokens expire mid-migration. The SDK handles this in `AuthenticatingHandler`:
when a request comes back `401`, it gets a new token and tries again.

Since the last release, the identity team has complained:

1. "Every hour, one desktop client sends **8 token requests within the same second**. We have rate limits.
   Some clients are getting throttled, and then their migrations fail."
2. "Last Tuesday one client sent **2.3 million** token requests in an afternoon. Its app registration had been
   misconfigured by the customer's admin. We issued it tokens, but the migration API rejected them."
3. "Customers whose account is missing the *Migration.Write* permission get a spinning progress bar
   instead of an error. The API correctly answers `403`."

Also, the first batch of uploads after starting the app makes one token request **per upload**.

## Your Task

Fix the authentication components so tokens are refreshed correctly and safely.

Ask whatever clarifying questions you think you need.

## Constraints

- Keep the constructors of `TokenProvider` and `AuthenticatingHandler` (they are wired up by the hosts).
  Other members can change.
- Requests must still be retried transparently after a token expires. Callers should not notice.

## Acceptance Criteria

- However many requests are in flight when the token expires, the client makes **one** token request to replace it.
- The first requests after start-up share one token request.
- If the platform keeps rejecting a freshly issued token, the SDK gives up quickly with `AuthenticationFailedException`.
- `403 Forbidden` is reported to the caller and does not cause token requests.

## How to Run

```bash
dotnet test exercises/06-authentication/06-01-token-expires-mid-migration/tests
```

## When You're Done

All tests pass, and you can explain what the difference between `401` and `403` means for the SDK.
