# 03-03 One Failure Hides the Others – Interviewer Notes

**Type:** async debugging · **Time:** 20 min · **Solution code:** `code/src/BatchUploader.cs`

## What is wrong

- `await Task.WhenAll(...)` **does** wait for all tasks, then rethrows **only the first** exception
  (the rest are in `whenAllTask.Exception.InnerExceptions`, which the code never looks at).
- The `catch` marks **every** file as failed with that one exception, so "Retry failed" resends successes (the duplicates)
  and the validation error is invisible (support chased the server error).
- Cancellation is swallowed and reported as a failure of every file.

## Hints

1. "When `WhenAll` throws, which exception do you get, and what happened to the other tasks?"
2. "Do you need exceptions to flow through `WhenAll` at all?"
3. "Wrap each upload so it returns an outcome instead of throwing, then `WhenAll` the outcomes."

## Intended solution

Per-file wrapper returning `FileOutcome` (catching everything except cancellation); `WhenAll` over those; throw if cancelled.

### Alternatives

- Keep the `WhenAll` task in a variable, `try { await t; } catch { inspect t.Exception.InnerExceptions }`, then map
  exceptions back to files by index. Works but awkward (mapping exceptions to files is error-prone).
- Inspect each original task's `IsFaulted`/`Exception` after `await Task.WhenAll(...).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing)` (.NET 8+). Neat. Ask if they know it.

## Common mistakes

- `Task.WhenAny` loops that return at the first failure (breaks "batch finishes only when all finished").
- Catching `AggregateException` (await unwraps to the first inner exception; `AggregateException` is never thrown here).
- Treating `OperationCanceledException` as a file failure (fails the cancellation test).
- `Task.WaitAll` (blocking; throws `AggregateException`).

## Follow-ups

- "If a file failed with `InvalidDataException`, should Retry failed resend it?" (No: a permanent error. Separate retryable from permanent outcomes; see 08-01.)
- "Why did the platform get duplicates? What would protect it?" (Idempotency keys; see 07-03.)
- "What should the UI show for 1 of 200 failed?"

## Scoring notes

| Level | Indicators |
|---|---|
| Weak | Believes `WhenAll` aborts on the first failure; catches `AggregateException`; loses cancellation |
| Solid mid-level | Per-file outcome wrapper; each error preserved; cancellation propagates |
| Strong | Explains `WhenAll` exception semantics precisely (first exception rethrown; `Task.Exception` holds all); distinguishes permanent vs retryable in "Retry failed" |
| Senior | Links to idempotency and duplicate prevention; UX of partial failure; telemetry per outcome |
