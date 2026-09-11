# 08-01 The Platform Has Bad Minutes – Interviewer Notes

**Type:** implementation (resilience) · **Time:** 30 min · **Solution code:** `code/src/MetadataUploader.cs`
**Spec scenario:** `503, 503, 200` recovers; `401` not retried forever; `400` permanent; `429` + `Retry-After`

## What the candidate must decide

| Decision | Good answer |
|---|---|
| What is transient? | 408, 429, 500, 502, 503, 504, connection failures, timeouts (not caller cancellation) |
| What is permanent? | 400, 404, 409, 422, and **401/403 here** (auth is handled by a dedicated component; blind retries of 401 are how 06-01's 2.3 million requests happened) |
| How long to wait? | Exponential backoff with **jitter**, a cap per delay, and a total **budget** |
| Server hints? | `Retry-After` (delta or HTTP date) wins over our own schedule, capped |
| Is retrying safe? | POST + `Idempotency-Key`, so yes. Without the key, retries risk duplicates |
| Cancellation? | `Task.Delay(delay, timeProvider, token)`: cancel ends the pause immediately |
| Request reuse? | New `HttpRequestMessage` per attempt |

## Hints

1. "Which of these status codes could possibly succeed if you sent the same request again?"
2. "If 2,000 desktops all get a 503 at the same second, what does a fixed 1-second retry do?"
3. "Loop: attempt → classify → delay (Retry-After, else exponential with jitter, capped) → stop on budget or attempts; every wait takes the `TimeProvider` and the token."

## Intended solution

See `code/`: `MaxAttempts = 6`, full-jitter backoff (1 s base, 30 s cap), `Retry-After` capped at 1 minute, 2-minute total budget.
The failure thrown at the end is the **last** failure, with its status code.

### Alternatives

- **Polly v8 / `Microsoft.Extensions.Http.Resilience`** (`AddStandardResilienceHandler`): retry with jitter, `Retry-After` support, circuit breaker, timeouts.
  Perfectly acceptable if they can explain what it's configured to do, and check that it's usable where the SDK runs
  (.NET Standard 2.0 consumers can use Polly; the `Microsoft.Extensions.Http.Resilience` handler needs `IHttpClientFactory`).
- A `DelegatingHandler` with the retry logic, so all SDK calls get it. Good. But then idempotency must be known per request (retry only idempotent methods or requests with a key).
- Decorrelated jitter instead of full jitter. Both fine; the point is to *de-synchronise* clients.

## Common mistakes

- Retrying everything (`catch (Exception)`), including 400 and 401.
- `Thread.Sleep` / `Task.Delay` without the `TimeProvider` → tests take minutes (and the test harness here can't advance them).
- Reusing the `HttpRequestMessage` (throws on attempt 2).
- Ignoring `Retry-After`, or trusting it unconditionally (a buggy server saying `Retry-After: 86400`).
- Budget checked only *after* sleeping (sleeps past the budget).
- Catching `OperationCanceledException` from the caller's token as "timeout, retry".

## Edge cases to discuss

- `Retry-After` as an HTTP date in the past → retry now.
- 503 with `Retry-After` vs 429 with `Retry-After`: same handling.
- A 500 that is actually deterministic (a bug for this payload): retries just waste time. It's bounded, which is the best a client can do.
- What should the *caller* see after exhausting retries? The last status, plus a hint that this was transient.

## Follow-ups

- "Several layers each retry 3 times. What's the worst case?" (Multiplication; see 08-03.)
- "The outage lasts 30 minutes. Who should retry: this method, the workflow, or the user?" (Layered responsibility: short retries here, resumable workflow later.)
- "How do you prove the jitter works?" (Simulation like 08-03; distribution tests with a seeded `Random`.)
- "Should the SDK expose the policy as configuration?" (Carefully: sensible defaults, a few knobs, no raw Polly objects in the public API.)

## Scoring notes

| Level | Indicators |
|---|---|
| Weak | Retries every failure with a fixed delay; blocks threads; can't classify 4xx vs 5xx |
| Solid mid-level | Correct classification, backoff, Retry-After, bounded attempts, cancellable delays, fresh request per attempt |
| Strong | Jitter with reasoning, total budget, Retry-After date and cap, idempotency awareness, explains every constant |
| Senior | Layering of retries across the SDK, retry budgets across clients, circuit breaking, observability of retries, library-vs-handwritten trade-offs for .NET Standard |

## Variants

- Change the outage to `503 ×8` (must fail within bounds).
- `429` with `Retry-After` as an HTTP date.
- Add a `Hang` step with an `HttpClient.Timeout` of 1 s (timeouts are transient).
