# 03-01 Five Hundred Files at Once – Interviewer Notes

**Type:** async implementation · **Time:** 25 min · **Solution code:** `code/src/BatchUploader.cs`
**Maps to:** special scenario **F (500 Concurrent Files)**

## What is wrong

`files.Select(async ...)` + `Task.WhenAll` **starts every upload immediately**: 500 concurrent requests, 500 open
file streams, 500 sets of buffers. The option is never read. Cancellation only reaches operations that already
started, and every one of them is running.

Walking through it step by step is a good question: `Task.WhenAll` enumerates the `Select`; each lambda runs
synchronously until its first `await`; by the time `WhenAll` has the array, all 500 are in flight.

## Hints

1. "How many uploads are running 1 ms after `UploadAllAsync` is called?"
2. "What would limit how many of those lambdas are allowed to *start*?"
3. "Something like a counting gate (a semaphore) around the upload, or a loop with a fixed number of workers. `Parallel.ForEachAsync` has this built in."

## Intended solution

`Parallel.ForEachAsync` with `MaxDegreeOfParallelism` and the cancellation token (.NET 6+).

### Alternatives

| Approach | Notes |
|---|---|
| `SemaphoreSlim(limit)` + `WaitAsync(ct)` inside each lambda, `Task.WhenAll` | Correct. Still allocates 500 tasks up front, each waiting on the semaphore; fine at 500, less so at 5 million. Must `WaitAsync(ct)`, **not** `Wait()`, and release in `finally` |
| N worker tasks draining a `ConcurrentQueue`/`Channel` | Correct; more code; good when producers are also async (see 04-03) |
| `Task.WhenAny` sliding window | Correct but fiddly; O(n²) if implemented naively with `List.Remove` |
| Batching (`Chunk(limit)` + `WhenAll` per chunk) | Bounded, but every batch waits for its slowest file, so throughput drops. A classic mid-level answer; ask about the slowest file |
| .NET Standard 2.0 SDK | No `Parallel.ForEachAsync`, so the semaphore or worker pattern it is. A good follow-up |

## Common mistakes

- `SemaphoreSlim.Wait()` (blocking) in async code.
- Forgetting `Release()` in `finally`, so one failure permanently shrinks the pool.
- `Parallel.ForEach` (sync) with an `async` lambda: `async void` all over again.
- Using `limit` to size `ThreadPool.SetMinThreads`.
- `MaxDegreeOfParallelism = 0`: `ParallelOptions` throws for 0; -1 means unbounded.

## Edge cases

- One upload fails: `Parallel.ForEachAsync` stops scheduling new items and throws. Is that the right policy? (Ask. It connects to 03-03.)
- `limit` changed at runtime by the user.
- Very uneven file sizes: 1 huge file + 499 tiny ones. Does ordering matter? (Largest-first reduces tail latency.)

## Follow-ups

- "What is the right default for `MaxConcurrentUploads`?" (Depends on bandwidth-delay product, server limits and proxies; typically 4–8; make it configurable; consider adaptive concurrency on 429.)
- "Where else does the SDK need a limit?" (Hashing is CPU and disk bound, so should it be a separate limit? Open file handles.)
- "How is this different from the per-host connection limit in `SocketsHttpHandler` (`MaxConnectionsPerServer`)?"

## Scoring notes

| Level | Indicators |
|---|---|
| Weak | Batches with `Chunk` without seeing the downside; blocking waits; can't explain why all 500 start |
| Solid mid-level | Semaphore or `Parallel.ForEachAsync` with cancellation; explains the original behaviour |
| Strong | Compares approaches (allocation, tail latency, failure policy); handles invalid option values |
| Senior | Discusses adaptive concurrency, server-side limits and 429 feedback, and where limits belong in the SDK architecture |
