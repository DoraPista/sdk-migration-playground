# 13-02 Tests for Retry and Cancellation – Interviewer Notes

**Type:** testing · **Time:** 30 min · **Solution code:** `code/src/`, `code/tests/`

## Hints

1. "Write the cancellation test first - cancel while an upload is in flight - and check what the platform is asked to do."
2. "The backoff uses real time. What would have to change in the class for a test to control the clock?"
3. "Inject a TimeProvider and delay through it, then look closely at which token the abort call is given."

## The defect the tests are supposed to find

```csharp
catch (OperationCanceledException)
{
    await _api.AbortSessionAsync(sessionId, cancellationToken); // <- the token that was just cancelled
    throw;
}
```

The abort is issued with the **already-cancelled** token, so `HttpClient` throws before the request leaves the
machine and the session is never released. That is the support ticket: 4,000 abandoned upload sessions.

Clean-up that runs *because* of cancellation must not be governed by the cancelled token. Use
`CancellationToken.None` with its own timeout (the solution uses `new CancellationTokenSource(10s, timeProvider)`).

There is a second leak most candidates miss: when the retries are **exhausted**, the session is abandoned too.
The solution moves the clean-up to one `catch` around the whole loop, which covers all three exits
(cancelled mid-send, cancelled during the backoff, out of attempts).

A third, subtler point: with the original structure, an `await` inside a `catch (HttpRequestException)` block
means the `OperationCanceledException` from the backoff delay is **not** caught by the sibling
`catch (OperationCanceledException)` — sibling catch clauses do not apply to exceptions thrown inside a catch
block. So even a candidate who fixes the token still leaks on "cancel during the backoff" unless they
restructure. The test `Cancelling_during_the_backoff_also_releases_the_session` pins that down.

## Making time testable

`Task.Delay(delay, TimeProvider, ct)` with an injected `TimeProvider` (defaulted to `TimeProvider.System`,
so no existing caller changes). Tests use `FakeTimeProvider` and
`AdvanceUntilCompleteAsync` from `Gym.TestUtilities`. Suite drops from ~15 s to well under a second.

Acceptable alternatives: an injected `Func<TimeSpan, CancellationToken, Task>` delay, an `IDelayStrategy`,
or exposing the backoff as an `IAsyncEnumerable<TimeSpan>` policy. Polly with a test-friendly clock also counts.
Not acceptable: making the delays configurable and setting them to zero in tests — that removes the behaviour
under test instead of controlling it, and it leaves a production-only code path untested.

## Tests a good answer has

| Test | What it protects |
|---|---|
| succeeds at the first attempt | the happy path, and that nothing is aborted |
| retries until accepted (4 attempts) | the retry count |
| the same content to the same session | no re-opening a session per attempt, no consumed stream |
| backoff is 1, 2, 4 seconds | the schedule itself, using the fake clock |
| gives up after four attempts, surfacing the failure | that it does not retry for ever |
| giving up releases the session | leak #2 |
| cancelling stops the upload | cancellation propagates |
| **cancelling releases the session** | the support ticket |
| cancelling during the backoff releases the session | the catch-block subtlety |
| a failing abort does not hide the cancellation | clean-up must not mask the real error |

## Common mistakes

- Testing the retry count by measuring wall-clock time (`Assert.True(sw.Elapsed > 7s)`) — slow and flaky.
- Asserting on `Thread.Sleep`-based fakes, or a `[Fact(Timeout = …)]` instead of a fake clock.
- Cancelling with `cts.CancelAfter(50)` — real time creeps back in; use a gate so the test controls the moment.
- Fixing the abort but swallowing the `OperationCanceledException` (the caller must still see cancellation).
- Letting the abort's own failure replace the original exception.
- Testing `AbortSessionAsync` was *called* without noticing that the call is doomed by the cancelled token: the
  fake must throw on a cancelled token (as the one here does) or the test passes while production leaks.

## Follow-up questions

- "The app is closing — is a 10 s abort acceptable?" (Probably not: record it and abort on next start, see 10-03.)
- "The abort fails. Now what?" (Record the session id and retry later; platform-side expiry as a backstop.)
- "How would you know in production that this was happening?" (A counter of sessions started vs closed, per migration.)

## Scoring notes

| Level | Indicators |
|---|---|
| Weak | Writes only the retry test; never reaches the cancellation path; keeps real delays |
| Solid mid-level | Injects `TimeProvider`, tests retry + give-up + cancellation, finds the abort token bug |
| Strong | Also covers the backoff schedule, the exhausted-retries leak, and abort failure |
| Senior | Talks about how clean-up should behave on shutdown, observability for the leak, and keeps the public API compatible |
