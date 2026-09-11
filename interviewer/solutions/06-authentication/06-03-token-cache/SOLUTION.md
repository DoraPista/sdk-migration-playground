# 06-03 The Token Cache – Interviewer Notes

**Type:** debugging + implementation · **Time:** 25 min · **Solution code:** `code/src/TokenCache.cs`

## What is wrong

| Problem report | Cause |
|---|---|
| 1. Contoso uploads went to Northwind's tenant | Cache key is `ClientId` only; tenant and scope are ignored. The same client registration serves many tenants. **A cross-tenant data incident** |
| 2. Long uploads fail with 401 | No safety margin: a token with 2 s left is handed out; the platform validates at completion |
| 3. Flaky around DST | `DateTime.Now` is **local** time. When clocks go back an hour, `ExpiresAt > DateTime.Now` stays true an hour too long, so expired tokens are handed out; when they go forward, tokens are renewed early. The injected `TimeProvider` is never used, so tests can't control time |
| 4. Bursts of token requests | Check-then-act across `await` with no single-flight: concurrent callers all miss and all request |
| (also) | `static` dictionary: shared across SDK instances (and test classes), and `Dictionary` is not thread-safe for concurrent writes (can corrupt or throw) |

The test clock deliberately starts at **2026-03-29 00:30 UTC**, the night the EU switches to summer time.

## Hints

1. "What's the key of the cache? What else distinguishes two tokens?"
2. "What time zone is `DateTime.Now` in? Where is `_time` used?"
3. "An instance-level `ConcurrentDictionary` keyed by the whole context, storing the *task* of the token request; use the injected clock in UTC with a 60 s margin."

## Intended solution

`ConcurrentDictionary<TokenRequestContext, Task<CachedToken>>` per instance; `GetOrAdd` shares the in-flight request;
usable only if `ExpiresAt - now >= 60 s` (UTC, from `TimeProvider`); remove failed or stale entries with the
`TryRemove(KeyValuePair)` overload so you never remove someone else's fresh entry.

### Alternatives

- `SemaphoreSlim` per key (`ConcurrentDictionary<key, SemaphoreSlim>`) + double-checked cache. Fine; watch the semaphore dictionary growing forever.
- `Lazy<Task<T>>` values. Common; same failure-caching caveat.
- `Microsoft.Extensions.Caching.Memory` with absolute expiration relative to `expires_in` − margin. Fine, but it doesn't solve the stampede on its own (`GetOrCreateAsync` isn't single-flight).
- MSAL's token cache, if the platform uses Entra ID.

## Common mistakes

- Switching to `DateTime.UtcNow` but still not using `TimeProvider` (tests still can't control time).
- Keying by `TenantId` only (scope still mixed) or by a string concatenation without separators (`"a|bc"` vs `"ab|c"` collisions). A record key avoids both.
- Caching a faulted task, so one network blip poisons the cache forever.
- Passing the first caller's `CancellationToken` into the shared request, so one cancelled caller fails all the others.
- A margin comparison using `>` instead of `>=` (arguable; be consistent).

## Follow-ups

- "How would you know problem 1 happened in production?" (Tenant ID in every upload's telemetry; server-side validation that the token's tenant matches the destination's tenant: defence in depth.)
- "Tokens are JWTs. Should the cache read `exp` from the token instead of `expires_in`?" (Either is fine; `expires_in` is relative, so client clock skew matters less.)
- "Could the cache persist tokens to disk to survive restarts?" (Probably not worth the risk; if yes, DPAPI-encrypted.)

## Scoring notes

| Level | Indicators |
|---|---|
| Weak | Fixes only the key, or only the time; keeps `static`; unaware of DST implications |
| Solid mid-level | Correct key, instance cache, injected UTC clock, margin, single-flight |
| Strong | Doesn't cache failures; cancellation isolation for the shared request; spots the thread-safety of `Dictionary` |
| Senior | Treats problem 1 as a security incident with server-side defence in depth; telemetry; clock skew considerations |
