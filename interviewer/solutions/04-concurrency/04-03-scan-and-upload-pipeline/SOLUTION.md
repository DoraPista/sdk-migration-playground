# 04-03 The Pipeline That Hangs – Interviewer Notes

**Type:** concurrency (producer/consumer) · **Time:** 30 min · **Solution code:** `code/src/ScanAndUploadPipeline.cs`

## What is wrong

| Symptom | Mechanism |
|---|---|
| 1. Scanner `IOException` → hang forever | The producer throws before `CompleteAdding()`. Consumers block in `GetConsumingEnumerable()` waiting for items that will never come. The producer's task faults, but **nobody awaits it** |
| 2. Cancel during scan → hang | The scanner honours the token and throws `OperationCanceledException`, which leads to the same missing `CompleteAdding()`. `GetConsumingEnumerable()` has no token, so consumers can't notice the cancellation either |
| 3. Upload failure → keeps going for hours | A failing consumer dies alone. The other consumers keep draining, and the producer keeps scanning. `WhenAll` reports the error only at the very end. (If *all* consumers die, the producer blocks forever in `Add` on a full queue: a leaked thread) |
| Also | `BlockingCollection` blocks thread-pool threads (4 workers plus 1 producer), which risks starvation when several pipelines run; and `using var queue` disposes the collection while the producer may still be blocked in `Add` |

## Hints

1. "In the scanner-failure case, what is each consumer doing, and what is it waiting for?"
2. "Who observes the producer's exception? What guarantees `CompleteAdding` runs?"
3. "Complete the queue in a `finally`, give every blocking/awaiting call the token, and cancel the whole pipeline on the first failure."

## Intended solution

- Bounded `Channel<T>` (async backpressure, no blocked threads).
- Producer: `try/catch` records the failure; `finally { writer.TryComplete(); }`.
- One linked `CancellationTokenSource` for the pipeline, cancelled by the caller **or** the first failure.
- Consumers: `ReadAllAsync(token)`; on failure record and cancel.
- Await producer **and** consumers; rethrow the *first real* failure (not the cancellations it caused), with `ExceptionDispatchInfo` to keep the stack trace.

### Alternatives

- Keep `BlockingCollection` but fix it: `CompleteAdding` in `finally`, `GetConsumingEnumerable(token)`, `Add(item, token)`, cancel on failure. Acceptable; the thread blocking remains a (discussable) cost.
- `Parallel.ForEachAsync(_scanner.Scan(root, ct), ...)`. It pulls lazily from the enumerable, so it is bounded and overlapping, and it stops on the first exception. **Very** clean. The enumeration happens on the thread requesting items, which serialises the scanner (fine here). A strong candidate might say "this whole class can be five lines".
- TPL Dataflow (`BufferBlock`/`ActionBlock` with `BoundedCapacity`, `PropagateCompletion`). Valid, heavier dependency.

## Common mistakes

- `CompleteAdding()` in `catch` only (cancellation paths still hang).
- An unbounded channel "to avoid deadlocks" breaks the bounded-memory test (and real memory on huge shares).
- Rethrowing `OperationCanceledException` from a consumer instead of the upload's `HttpRequestException` (loses the root cause).
- Forgetting to await the producer (its failure is lost or surfaces as an unobserved exception).
- Catching per file and continuing (contradicts the product decision in symptom 3; ask whether they agree with it).

## Follow-ups

- "Product changes its mind: skip files that fail and report them at the end. What changes?" (Per-file outcomes; see 03-03.)
- "How do you pick the queue capacity?" (Bounded by memory per item × capacity; large enough to smooth scanner hiccups.)
- "The scanner is also slow for single folders with 500k entries (`FindFirstFile` over SMB). Anything to improve?" (Enumeration options, parallel directory walks; or ask for an index/export from the source system.)

## Scoring notes

| Level | Indicators |
|---|---|
| Weak | Adds timeouts; can't explain which party waits for which; makes the queue unbounded |
| Solid mid-level | `finally`-complete; token everywhere; pipeline-wide cancel on first failure; awaits producer |
| Strong | Channels and async backpressure; root-cause exception selection; explains thread-pool blocking |
| Senior | Recognises `Parallel.ForEachAsync` as a simpler design; discusses failure policy with product; capacity sizing and observability |
