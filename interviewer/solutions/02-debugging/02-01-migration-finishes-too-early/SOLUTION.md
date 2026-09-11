# 02-01 The Migration Finishes Too Early – Interviewer Notes

**Type:** debugging · **Time:** 20 min · **Solution code:** `code/src/MigrationRunner.cs`

## What is wrong

| # | Defect | Customer symptom |
|---|---|---|
| 1 | Workers are started with `_ = Task.Run(...)`: fire-and-forget, never awaited | Nothing waits for the uploads |
| 2 | "Done" is detected as `_queue.IsEmpty`. The queue empties as soon as the **last file is dequeued**, while up to `workerCount` uploads are still in flight | Report 1 (complete, then files keep appearing) and report 2 (platform completes, late files quarantined) |
| 3 | An exception in `UploadAsync` ends that worker and faults its task, which nobody observes. `_failed` is never incremented | Report 3 (share offline, "0 failed", success) |
| 4 | `_uploaded++` from several threads: a non-atomic read-modify-write | Occasionally wrong counts (rare, non-deterministic; bonus point) |
| 5 | Queue and counters are instance fields | A second `RunAsync` on the same runner reports the previous run's numbers (bonus) |
| 6 | Polling with `Task.Delay(50)` | Wasted latency and CPU; a symptom of not having the tasks |

## Hints

1. "When exactly does `_queue.IsEmpty` become true?"
2. "What happens to the `Task` returned by `Task.Run` here? What happens to an exception thrown inside it?"
3. "Keep the worker tasks and await all of them; catch per-file failures inside the worker loop and record them."

## Intended solution

- Keep the worker tasks, `await Task.WhenAll(workers)`.
- Per-file `try/catch` inside the worker that records a `FileFailure` and continues. Cancellation is **not** a file failure.
- Complete on the platform only when there are no failures.
- `Interlocked.Increment` (or count from a results collection); per-run local state.

### Alternatives

- `Parallel.ForEachAsync(files, new ParallelOptions { MaxDegreeOfParallelism = workerCount, CancellationToken = ct }, ...)`.
  This is the idiomatic .NET 6+ answer (not available on .NET Standard 2.0 without a package; good follow-up).
- `SemaphoreSlim` + `Task.WhenAll(files.Select(...))`. Fine for moderate file counts; creates one task per file up front.
- A `Channel<SourceFile>` with N readers. Also fine, and more machinery than needed here.

## Common mistakes

- Replacing `IsEmpty` with a counter `while (_uploaded < files.Count)`. It hangs forever when a file fails.
- `Task.WaitAll(...)`, blocking inside async code.
- `await Task.WhenAll(...)` **without** per-file handling: the first failure throws, and the remaining successes are lost from the summary (see exercise 03-03).
- Catching `Exception` and swallowing `OperationCanceledException` as a "failed file".
- Leaving `FileUploaded` invocation inside the `try`, so a subscriber exception marks the file as failed (debatable; worth discussing).

## Follow-up questions

- "Why didn't the unit tests the team had catch this?" (Fast fakes complete before the 50 ms poll.)
- "What would you log when a file fails? What would the user see?"
- "The platform quarantines late files. What would a server-side safeguard look like?" (Complete only when the manifest count matches; or reject completion while uploads are in flight.)
- "How would you make `FileUploaded` safe for a WPF subscriber?" (See 11-02.)

## Scoring notes

| Level | Indicators |
|---|---|
| Weak | Adds a longer delay, or `Thread.Sleep`; doesn't find the unobserved exception; can't explain why the queue is empty early |
| Solid mid-level | Awaits the workers; per-file failure recording; no completion on failure; explains all three reports |
| Strong | Spots the counter race and per-instance state; distinguishes cancellation from failure; mentions `Parallel.ForEachAsync` |
| Senior | Talks about the platform contract (completion semantics, late files), observability, and how to test concurrency deterministically (gates, like the tests do) |

## Variants

- Change `workerCount` to 1: the "early completion" window shrinks to one file (same bug, subtler).
- Throw `OperationCanceledException` from a fake that was *not* cancelled (a timeout). Should it be a failure?
