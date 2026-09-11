# 02-04 The Desktop App Hangs on "Connect" – Interviewer Notes

**Type:** debugging (sync-over-async deadlock) · **Time:** 20 min · **Solution code:** `code/src/MigrationClient.cs`

This is the classic, and it matters here because **the SDK is consumed by WPF apps**.
A mid-level candidate with 4.5 years of WPF should know it cold. The interesting part is the fix *in a library*.

## What is wrong

1. **Deadlock.** `Connect()` calls `ConnectAsync().Wait()` on the UI thread. Inside `ConnectAsync`, `await _http.PostAsync(...)`
   captures the WPF `DispatcherSynchronizationContext`. When the HTTP call completes, the continuation is **posted to the UI thread**,
   which is blocked in `.Wait()` waiting for that continuation. Each side waits for the other.
   It works in a console harness because there is no synchronization context, so continuations run on the thread pool.
2. **Wrong exception type.** `.Wait()` / `.Result` wrap failures in `AggregateException`, so the app's `catch (HttpRequestException)` never matches and the app crashes.
3. *(Secondary)* `_http.DefaultRequestHeaders.Authorization = ...` mutates a possibly shared `HttpClient`. Not thread-safe; leaks the token to other users of the client.

## Hints

1. "What is different about a WPF button handler compared to the console harness?"
2. "Where does the code after `await _http.PostAsync(...)` want to run, and what is that thread doing?"
3. "Library code should not need the caller's context: `ConfigureAwait(false)`. And look at how `.Wait()` reports exceptions."

## Intended solution

- `ConfigureAwait(false)` on **every** await in the SDK (one missed await is enough to deadlock).
- Sync wrappers use `GetAwaiter().GetResult()` to unwrap the original exception; mark them `[Obsolete]` with guidance.
- Per-request `Authorization` header.

### Alternatives (and their costs)

| Approach | Notes |
|---|---|
| `Task.Run(() => ConnectAsync()).GetAwaiter().GetResult()` in the sync wrapper | Works without touching every await, because the async code starts on the thread pool. It still blocks the UI thread and costs a thread-pool thread. A reasonable *tactical* fix |
| Making the WPF callers async | The real fix. Out of scope by constraint, but a strong candidate says so |
| Removing the sync API | Breaking change; see 09-05 |
| `ConfigureAwait(false)` only at the top-level call | Doesn't work: inner awaits still capture |

The distinction to probe: the fix removes the **deadlock**. `Connect()` still **blocks** the UI thread while the network call runs, and the window is unresponsive for that time.

## Common mistakes

- Adding `ConfigureAwait(false)` inside `CheckHealthAsync` but not in `ConnectAsync`'s first await (still deadlocks).
- Believing `ConfigureAwait(false)` in the SDK makes the *caller's* code resume on a background thread (it doesn't; the test `Awaiting_ConnectAsync_on_the_ui_thread_resumes_on_the_ui_thread` demonstrates this).
- Wrapping in `Dispatcher.Invoke` or `DoEvents`-style pumping.
- `async void Connect()` (the caller can't observe completion or errors).

## Follow-ups

- "Does ASP.NET Core have this problem?" (No synchronization context in ASP.NET Core; classic ASP.NET did.)
- "What about thread-pool starvation from sync-over-async in a server?" (Many blocked threads, slow thread injection.)
- "Would you ship a sync API in a new SDK at all?"
- "How would you catch a missing ConfigureAwait in CI?" (Analyzers: CA2007 / ConfigureAwaitChecker; tests with a single-threaded context like these.)

## Scoring notes

| Level | Indicators |
|---|---|
| Weak | "Use Task.Run" without explaining why; can't describe the two waiting parties; thinks `ConfigureAwait(false)` affects the caller |
| Solid mid-level | Explains the deadlock precisely; `ConfigureAwait(false)` throughout the library; fixes AggregateException with `GetAwaiter().GetResult()` |
| Strong | Distinguishes deadlock vs blocking; spots the shared-header mutation; suggests `[Obsolete]` + migration plan |
| Senior | Talks about SDK guidelines (library code never captures context), analyzers, testing with a single-threaded context, and the API-evolution strategy for removing sync APIs |
