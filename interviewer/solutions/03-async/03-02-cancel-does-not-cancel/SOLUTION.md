# 03-02 Cancel Doesn't Cancel – Interviewer Notes

**Type:** async debugging + redesign · **Time:** 35 min · **Solution code:** `code/src/MigrationEngine.cs`
**Maps to:** special scenario **G (Cancellation)**

## What is wrong

| # | Code | Effect |
|---|---|---|
| 1 | `Task.Run(() => UploadWithRetryAsync(...), cancellationToken)` | The token only prevents the *delegate from starting*. It never reaches the work |
| 2 | `UploadWithRetryAsync` / `UploadOnceAsync` take no token; `PostAsync(url, content)` without a token | Active HTTP requests run to completion (or to `HttpClient.Timeout`, 100 s by default). Report 1 |
| 3 | `throttle.WaitAsync()` without a token | Queued files still start after cancel (each fails or runs). Report 1 ("keeps uploading") |
| 4 | `catch (Exception) when (attempt < max)` + `Task.Delay(delay)` without a token | Retries continue after cancel; the back-off can't be interrupted. Report 2 |
| 5 | `catch (TaskCanceledException) { cancelled = true; }` | `HttpClient.Timeout` also throws `TaskCanceledException`, so a slow network is reported as "cancelled by user". Report 3 |
| 6 | `_http.Timeout = ...` in the constructor | Mutates a possibly shared `HttpClient` (throws if it has already been used); a whole-request timeout can't distinguish attempts |
| 7 | `cancelled` flag written from several tasks without synchronisation | Minor |

## Hints

1. "Follow the `CancellationToken` from `RunAsync` down. Where does it stop?"
2. "What exception does `HttpClient` throw when *its own* timeout fires? How would you tell that from the user cancelling?"
3. "Pass the token everywhere (WaitAsync, Delay, SendAsync). Per attempt, use a linked CTS with `CancelAfter`, and decide 'cancelled' only by checking the caller's token."

## Intended solution

- One token flows through `Parallel.ForEachAsync` (or semaphore `WaitAsync(ct)`), the retry loop, `Task.Delay(delay, ct)` and `SendAsync(..., ct)`.
- Per attempt: `CreateLinkedTokenSource(ct)` + `CancelAfter(RequestTimeout)`; when an `OperationCanceledException` arrives and the **caller's** token is *not* cancelled, it was a timeout, so convert it into a `TimeoutException` (transient, retryable).
- Outcome `Cancelled` **only** when `cancellationToken.IsCancellationRequested`.

### Alternatives

- Keep `HttpClient.Timeout` but check `ex.InnerException is TimeoutException`. Since .NET 5, a timeout's `TaskCanceledException` wraps a `TimeoutException`.
  Valid and simpler; but a whole-client timeout is still shared state, and it isn't available on .NET Framework consumers of a .NET Standard SDK.
- Polly (`AddResilienceHandler` / `Microsoft.Extensions.Http.Resilience`) with timeout + retry strategies. Fine if they can explain what it does with cancellation.

## Common mistakes

- `cts.Token.ThrowIfCancellationRequested()` at the start of each file only (active uploads still run).
- Catching `OperationCanceledException` in the retry filter and retrying it.
- Treating *any* `OperationCanceledException` as user cancellation (report 3 survives).
- `Task.WhenAny(work, Task.Delay(timeout))` "timeouts" that abandon the work instead of cancelling it (the upload continues in the background).
- Forgetting that `Parallel.ForEachAsync` throws `OperationCanceledException` (not `TaskCanceledException`) on cancellation.

## Follow-ups

- "The server received 60% of a file when the user cancelled. What should happen on the server?" (Abort an upload session, or let it expire; see 07-02.)
- "Should Cancel wait for active uploads to finish their current file (graceful) or abort (prompt)?" (A product decision. Ask. Some users want 'finish current file then stop'.)
- "How do you test cancellation deterministically?" (Hanging handlers that observe the token, as in the tests, rather than sleeps.)
- "What happens to `FileStream` reads when cancelled?" (Async file I/O on Windows honours cancellation; on some platforms it only takes effect between reads.)

## Scoring notes

| Level | Indicators |
|---|---|
| Weak | Adds `ThrowIfCancellationRequested` at the top; doesn't notice the `Task.Run` token misconception; can't explain report 3 |
| Solid mid-level | Token everywhere including `WaitAsync`/`Delay`; explains the Task.Run token; separates timeout from cancellation |
| Strong | Per-attempt linked CTS; decides "cancelled" from the caller's token only; stops mutating the shared HttpClient |
| Senior | Discusses graceful vs prompt cancel, server-side cleanup, cancellation vs timeout semantics in public SDK APIs, and testing strategy |

## Variants

- Set `MaxConcurrency = 1` and cancel during the 2nd of 3 files.
- Make the slow file the *first* one (does it block the others? It shouldn't).
