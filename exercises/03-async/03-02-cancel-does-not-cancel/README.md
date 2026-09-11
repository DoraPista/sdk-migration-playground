# Exercise 03-02 – Cancel Doesn't Cancel

Difficulty: Hard
Estimated Time: 35 minutes

## Skills

- cancellation propagation
- timeouts vs cancellation
- retries and cancellation
- async control flow under failure

## Scenario

The desktop app has a **Cancel** button. Pressing it calls `Cancel()` on the `CancellationTokenSource`
that was passed to `MigrationEngine.RunAsync`. The UI immediately says "Cancelling…".

From the field:

1. "After pressing Cancel the app shows *Cancelling…* for a minute or more. Sometimes it looks like
   it keeps uploading."
2. "If the network is bad, Cancel doesn't do anything for ages."
3. "Some users see *Migration cancelled by user* even though **nobody pressed Cancel**. It happens on
   slow connections."

## Your Task

Make cancellation reach the actual work, and make the engine report timeouts and cancellations
correctly.

## Constraints

- Keep the public API of `MigrationEngine` and `MigrationEngineOptions`.
- `RequestTimeout` is the limit for a single HTTP attempt.
- A timed-out attempt is a transient failure and should be retried (up to `MaxAttempts`).

## Acceptance Criteria

- After cancellation, `RunAsync` returns promptly (well under a second in the tests) with outcome `Cancelled`, including during a retry delay.
- No new uploads start after cancellation.
- A file whose attempts all time out is reported as **failed**, not cancelled.

## How to Run

```bash
dotnet test exercises/03-async/03-02-cancel-does-not-cancel/tests
```

## When You're Done

All tests pass, and you can trace the path from the Cancel button to the socket.
