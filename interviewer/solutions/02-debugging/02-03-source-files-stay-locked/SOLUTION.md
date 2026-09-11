# 02-03 Source Files Stay Locked – Interviewer Notes

**Type:** debugging (resource leaks) · **Time:** 25 min · **Solution code:** `code/src/`

## What is wrong

### Ticket 1: locked CAD files

`FileUploader.UploadAsync` disposes the `FileStream` only on the happy path (`stream.Dispose()` after
`EnsureSuccessStatusCode()`). When the upload throws (network error, 5xx, cancellation), the handle stays open
until the finalizer runs, which can be much later in a process that doesn't allocate much. Restarting
the app releases it: the process exits and the OS closes all handles.
`HttpRequestMessage` and `HttpResponseMessage` aren't disposed either.

### Ticket 2: memory growth, and uploads for old migrations

`MigrationSession` subscribes to the **app-lifetime** `NetworkMonitor.ConnectivityChanged` and never
unsubscribes. The event's delegate list holds a strong reference to each session. Consequences:

- Every session ever created stays in memory (with its file list and uploader).
- **Old sessions still react**: when Wi-Fi comes back, every old session with pending files retries,
  which is the burst the platform sees. `Dispose()` sets a flag that nothing reads.

Secondary: `async void` handler with no guard (an unexpected exception would crash the process);
`_pending` is modified from the handler and from `RunAsync` without synchronisation; no cancellation of retries.

## Hints

1. Ticket 1: "Walk through `UploadAsync` when `SendAsync` throws. What happens to `stream`?"
2. Ticket 2: "Who holds a reference to a `MigrationSession` after the UI is done with it?"
3. "`using` for every disposable; unsubscribe in `Dispose`; make the handler check whether the session is still alive."

## Intended solution

- `await using` / `using` for stream, request and response.
- `Dispose()` unsubscribes, cancels in-flight retries, and is idempotent.
- The handler guards against disposal and exceptions, and doesn't run overlapping retry passes.

### Alternatives

- A weak-event pattern (`WeakEventManager` in WPF, or a hand-rolled weak subscription) is valid when the lifetime is hard to control.
  But the session is `IDisposable`; explicit unsubscribe is simpler and deterministic. Ask them to compare.
- Pull instead of push: the session asks the monitor (`WaitForOnlineAsync`) inside `RunAsync`, so no long-lived subscription is needed.
- `try/finally` instead of `using`. Equivalent, more verbose.

## Common mistakes

- Adding `stream.Dispose()` in a `catch` block but not for cancellation or other exception types.
- Unsubscribing, but still letting a handler already queued (connectivity event during Dispose) start an upload.
- Using a finalizer (`~MigrationSession`) to unsubscribe. It never runs, because the subscription keeps the object reachable.
- Making `NetworkMonitor` hold weak references *and* not fixing disposal.

## Why the tests are deterministic

- The locked-file test opens the file with `FileShare.None` immediately after the failed upload: no GC has run.
- The GC test creates the session in a non-inlined frame and forces a full collection. The only root is the monitor's event.

## Follow-ups

- "How would you find this leak in production?" (Memory dump: `dotnet-gcdump` / Visual Studio diagnostics → retention path to the monitor's delegate list; `dotnet-counters` for heap growth; Process Explorer / `handle.exe` for the file handle.)
- "Why are event leaks so common in WPF apps?" (Long-lived services and short-lived ViewModels; see 11-03.)
- "Should `FileUploader` open the file with `FileShare.Read` or `ReadWrite`?" (Trade-off: letting engineers save while uploading vs. uploading a torn file. Detect with a size/hash check.)

## Scoring notes

| Level | Indicators |
|---|---|
| Weak | Finds only one ticket; suggests `GC.Collect()`; doesn't know `using` covers exceptions |
| Solid mid-level | Fixes disposal on all paths; unsubscribes in `Dispose`; explains why restart "fixes" the lock |
| Strong | Guards the `async void` handler; notes the undisposed request/response; explains the retention path precisely |
| Senior | Discusses diagnosing leaks in the field (dumps, retention paths, handle tools), share modes and torn reads, and pull vs push designs for connectivity |
