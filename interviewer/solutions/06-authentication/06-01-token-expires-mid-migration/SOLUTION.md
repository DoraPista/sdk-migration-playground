# 06-01 The Token Expires in the Middle of a Migration – Interviewer Notes

**Type:** debugging + design (auth, concurrency) · **Time:** 40 min · **Solution code:** `code/src/`
**Maps to:** special scenario **B (Token Expiration During Migration)**

## What is wrong

| Complaint | Mechanism |
|---|---|
| 8 token requests in the same second | Each in-flight request gets its own 401 and calls `RefreshTokenAsync`, which **unconditionally** requests a new token. That's a refresh stampede. With token rotation, each new token may even invalidate the previous one, so the stampede can feed itself |
| 2.3 million token requests | After a refresh, `SendAsync` **calls itself recursively** without a limit. If the API rejects even fresh tokens (misconfigured registration, wrong audience, clock skew), it loops forever, requesting a token every iteration |
| Spinning progress on 403 | 403 is treated like 401, so the handler refreshes and recurses forever. **403 ≠ expired token** |
| One token request per upload at start-up | `if (_token is null) _token = await ...` is check-then-act across an await, so concurrent first callers all request tokens |
| (Latent) | Tokens are only renewed *reactively*, which costs one guaranteed failed request per hour per worker. No early renewal. `expires_in` is ignored |

## Hints

1. "Eight requests get a 401 at the same moment. Walk me through what each one does."
2. "What stops this handler from retrying forever? And what does 403 mean compared with 401?"
3. "Remember *which* token was rejected. Under an async lock, only refresh if that token is still the current one. Retry once; if the fresh token is rejected too, fail."

## Intended solution

- `TokenProvider`: `SemaphoreSlim` single-flight. `RefreshTokenAsync(rejectedToken)` only contacts the identity service if the rejected token is still current.
  `GetTokenAsync` is double-checked and renews **proactively** before expiry (`expires_in` − margin).
- `AuthenticatingHandler`: refresh on **401 only**; exactly **one** retry; persistent 401 → `AuthenticationFailedException`; 403 → returned to caller.

### Alternatives

- `Lazy<Task<string>>` / shared `Task` for the in-flight refresh (like 04-02). Equivalent; watch for caching a *faulted* refresh forever.
- Proactive-only renewal (a timer refreshes before expiry). Reduces 401s, but you **still** need reactive handling (revocation, clock skew, server restarts).
- `Microsoft.Identity.Client` (MSAL) for real Entra ID: it caches and refreshes for you. Valid in production; a candidate should still be able to explain the concurrency rules.
- Retry budget: allow a refresh-retry at most once per N seconds to guard against pathological servers.

## Common mistakes

- `lock` around `await` (doesn't compile), or `SemaphoreSlim` held for the **whole request** (serialises all uploads).
- Single-flight refresh but **no** check of which token failed: the second waiter refreshes again after the first finished (2 → 3 token requests).
- A retry counter stored in a field (shared by all requests) instead of per request.
- Refreshing on 403.
- Resending a request whose content is a non-seekable stream (works in these tests with `ByteArrayContent`; fails with forward-only streams). Ask about it.
- Caching a faulted refresh task forever, so the client can never recover after the identity service is briefly down.

## Clarifying questions worth rewarding

- Does the identity service rotate tokens (a new token invalidates the old)? Is there a refresh token, or only client credentials?
- How long are tokens valid, and is clock skew between client and server a concern?
- Is 401 always "expired", or can it mean "wrong audience" or "disabled client"? (Look at `WWW-Authenticate: Bearer error="invalid_token"`.)
- What should the user see when auth fails permanently?

## Follow-ups

- "The identity service is down for 5 minutes. What happens to 8 workers?" (They should wait with backoff, not stampede; see 08-03.)
- "Where does the client secret live on a customer's desktop?" (DPAPI/Windows Credential Manager/Keychain; better: interactive user auth with PKCE or managed identity; SAS URLs for data; see 16-04.)
- "How would you test clock skew?" (FakeTimeProvider; server `nbf`/`exp` claims.)

## Scoring notes

| Level | Indicators |
|---|---|
| Weak | Adds `try/catch` or a counter field; doesn't see the recursion; treats 403 as expiry |
| Solid mid-level | Single-flight refresh with rejected-token check; one retry; 401 vs 403 correct; start-up stampede fixed |
| Strong | Proactive renewal with margin; explains non-replayable request content; failure caching pitfalls |
| Senior | Token rotation dynamics, identity-service protection (budgets/backoff), secret storage on desktops, MSAL/managed identity trade-offs |
