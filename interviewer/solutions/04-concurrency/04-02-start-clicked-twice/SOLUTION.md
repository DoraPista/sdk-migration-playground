# 04-02 "Start Migration" Clicked Twice – Interviewer Notes

**Type:** concurrency · **Time:** 25 min · **Solution code:** `code/src/MigrationController.cs`
**Maps to:** special scenario **D (Duplicate Start)**

## What is wrong

A check-then-act race across an `await`:

```
click 1: IsRunning? no → await CreateMigrationAsync (3 s)...
click 2: IsRunning? no (_current is still null) → await CreateMigrationAsync
```

`_current` is only assigned **after** the slow call returns, so the guard protects nothing during the window that matters.
Because both calls run on the UI thread, there's no multithreading here at all. It's an **async** race.
Many candidates assume "single UI thread = no races". That's a great point to probe.

## Hints

1. "What is `_current` during the three seconds that `CreateMigrationAsync` takes?"
2. "What could you store *immediately*, before the slow call finishes, that a second caller could wait on?"
3. "Store the `Task` of the start operation itself (under a lock), and hand the same task to every caller until it's finished or failed."

## Intended solution

Keep the **in-flight task** (`Task<MigrationHandle>`) and set it atomically under a short lock. Later callers get the same task.
A faulted/cancelled start or a finished migration clears the way for a new start.

### Alternatives

| Approach | Verdict |
|---|---|
| `SemaphoreSlim(1,1)` around the whole start, then check `_current` | Works. The second click *waits*, then sees the running migration. Make sure it doesn't hold the semaphore for the whole migration |
| `Lazy<Task<MigrationHandle>>` | Works for "only once ever", but must be replaced after completion **and after failure**. Many solutions forget the failure case (fails `If_starting_fails_the_user_can_try_again`) |
| `Interlocked.CompareExchange` on a `TaskCompletionSource` | Correct and lock-free; more subtle |
| UI-only fix: disable the button / `CanExecute` | Necessary for UX, insufficient for an SDK (scripts, other hosts, re-entrancy before `CanExecute` re-evaluates) |
| Server-side: `Idempotency-Key` on `POST /migrations` (supported by the mock server) | **The** answer for two app instances or two machines; the local fix covers one process only. Senior candidates raise it unprompted |

## Common mistakes

- Setting `_current = new MigrationHandle(..)` *before* the await with a placeholder ID. Then what does a second caller get?
- Holding a `lock` across the await (doesn't compile), or `SemaphoreSlim.Wait()` (blocks the UI thread).
- Caching failures forever (`Lazy` without reset).
- Thinking `ConfigureAwait(false)` or `Task.Run` is related.
- Cancel semantics: the second click passes its own token, which is ignored by the shared start. Is that OK? (Ask. A "join" shouldn't be able to cancel the owner's operation.)

## Clarifying questions worth rewarding

- Same customer only, or any customer? What if a different customer is started while one is running? (Probably reject with a clear error.)
- Does "start" survive the app restarting? (Then local state isn't enough; resume from the server; see 10-03.)
- Should the second click *join*, *throw*, or be *ignored*? (This README says join.)

## Follow-ups

- "Two machines start the same migration. What prevents duplicates?" (Idempotency key derived from customer + source + intent; or a server-side unique constraint per customer with 409 → return existing, as in 08-02.)
- "How would the idempotency key be generated so that a *retry after a crash* reuses it?" (Persist it with the local intent before calling the server.)

## Scoring notes

| Level | Indicators |
|---|---|
| Weak | "Disable the button"; adds a bool flag set after the await; thinks single-threaded UI can't race |
| Solid mid-level | Shares the in-flight task (or semaphore) atomically; handles completion and failure resets |
| Strong | Explains async races on a single thread; discusses join vs reject; token ownership of the shared operation |
| Senior | Moves the guarantee to the server with idempotency keys; persistence of intent; cross-process/machine scenarios |
