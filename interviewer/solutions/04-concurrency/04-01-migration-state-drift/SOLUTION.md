# 04-01 The Checkpoint Drifts – Interviewer Notes

**Type:** concurrency debugging · **Time:** 30 min · **Solution code:** `code/src/`
**Spec requirement:** "passes simple tests but fails under concurrent execution, deterministically"

## What is wrong

`MarkUploadedAsync` is a **read-modify-write across `await`s**:

```
worker A: Load → {F1..F9}       worker B: Load → {F1..F9}
worker A: add F10, Save {..F10}
                                worker B: add F11, Save {..F11}   ← F10 is lost
```

- **Lost updates:** the last saver wins with a state based on an old read. The checkpoint "forgets" uploaded files,
  so after a crash they're re-uploaded, and the checkpoint shows 3 while the platform has 40.
- **Progress goes backwards:** each worker reports `state.UploadedFileIds.Count` from *its own stale copy*.
- More concurrency means a larger window, so it gets worse with higher `MaxConcurrentUploads`.
- `lock` can't be used around `await`s, which is probably why nobody added one.

**Why the tests never caught it:** they call the tracker sequentially. Also, an in-memory fake that returns the
*same object instance* hides the bug entirely: every "load" sees every other worker's mutations. The test fake here
returns a *copy of what was last saved*, like real storage does, and delays a little. Because every concurrent call reads its
snapshot synchronously before the first `await`, the failure is **deterministic**, not timing-dependent.

## Hints

1. "Two workers call this at the same time. Walk me through what each one reads and writes."
2. "What would you normally use to make a read-modify-write atomic, and why doesn't it work here?"
3. "An async-compatible mutual exclusion around the whole load-modify-save sequence, or a single writer that owns the state."

## Intended solution

`SemaphoreSlim(1, 1)` around load → modify → save → notify. Release in `finally`. `WaitAsync(cancellationToken)`.
Also: make the file store's save atomic (temp file + `File.Move(..., overwrite: true)`).

### Alternatives (discuss the trade-offs)

| Design | Trade-off |
|---|---|
| **In-memory authoritative state** + lock (sync `lock` is fine, since there's no await inside), persisted by a single background writer that coalesces saves | Best throughput: no load per update, and fewer disk writes. Must flush on completion/cancel, and a crash loses the last few hundred ms (acceptable if uploads are idempotent; see 07-03) |
| `Channel<Update>` with one consumer (actor style) | Clean ownership; ordering guaranteed; more code |
| `ConcurrentDictionary` for the IDs + serialized saves | Fixes the in-memory part but still needs save ordering (saves can race and reorder) |
| Append-only log (one line per uploaded file) | Very robust to crashes, and no read-modify-write at all; needs compaction |
| Optimistic concurrency (version/ETag, retry on conflict) | What you'd do with a remote store (Azure Table/Cosmos); overkill for a local file |

## Common mistakes

- `lock (_sync) { ... await ... }`: compile error, and then people try `Monitor.Enter` + await (thread-affinity bug).
- Locking only the modification (in-memory `Add`), not the load and save.
- `ConcurrentBag`/`ConcurrentDictionary` inside the loaded state: the object is a fresh copy per load, so thread-safe collections don't help.
- Raising `ProgressChanged` outside the lock with the count captured inside. That's OK for correctness, but notifications can reorder.
- `SemaphoreSlim.Wait()` (blocking).

## Follow-ups

- "Now there are two *processes* (desktop app + a background service) writing the same file." (File locks or a single owner process; or move state to the server; see 10-03 and 16-03.)
- "The store is a network share and saves take 300 ms. With 16 workers the lock becomes a bottleneck. What now?" (Coalescing single writer.)
- "Which is the source of truth: local checkpoint or server?" (A key design question: reconcile with the server on resume.)
- "How would you find this in production?" (Checkpoint vs server count mismatches in telemetry; logs with worker IDs; reproduce with more workers.)

## Scoring notes

| Level | Indicators |
|---|---|
| Weak | Adds `lock` around part of the method, or `ConcurrentBag`; can't explain the interleaving |
| Solid mid-level | Explains lost update across awaits; `SemaphoreSlim` around the whole RMW with `finally`; progress fixed |
| Strong | Explains why the original fake hid it; notices non-atomic file saves; discusses notification ordering |
| Senior | Proposes single-writer / coalescing designs; source of truth and reconciliation; multi-process ownership; append-only logs |

## Variants

- Increase the store's save delay and ask about throughput.
- Use a store whose `LoadAsync` returns the same instance (the bug disappears in tests). Ask the candidate why.
