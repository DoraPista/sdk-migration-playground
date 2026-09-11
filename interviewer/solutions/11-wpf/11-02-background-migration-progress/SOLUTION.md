# 11-02 Progress From a Background Migration – Interviewer Notes

**Type:** WPF threading + throughput · **Time:** 25 min · **Solution code:** `code/src/MigrationViewModel.cs`

## What is wrong

| Symptom | Cause |
|---|---|
| 1. `NotSupportedException` | `Files.Add(...)` runs on the SDK's worker thread. A collection bound to an `ItemsControl` may only change on the dispatcher thread. (Scalar properties are different: WPF marshals `PropertyChanged` for you. That asymmetry is the thing to know.) |
| 2. The migration got slower | `Application.Current.Dispatcher.Invoke(...)` **blocks the worker** until the UI thread is free. With a busy UI, every progress event waits |
| 3. Sluggish UI | One UI update per progress event: thousands of layout/render passes per second |
| (also) | `Application.Current` is null in tests and in any host that isn't a WPF app; the view model should capture *its own* dispatcher |

## Hints

1. "Which thread raises `FileUploaded`, and which thread is allowed to touch `Files`?"
2. "What is the difference between `Invoke` and `BeginInvoke` for the *worker*?"
3. "The UI can't usefully show 5,000 updates a second. What if events were collected and applied a few times per second?"

## Intended solution

- Capture `Dispatcher.CurrentDispatcher` when the view model is created (the UI thread), instead of `Application.Current.Dispatcher`.
- Worker-side handlers only **record**: enqueue the file name, store the latest percentage. No UI work, no blocking.
- Post a single flush (`BeginInvoke`) at most once per refresh interval (~50 ms); the flush drains the queue and sets `Percent` once.
- Flush again when the run completes, so the final state is correct.

### Alternatives

| Approach | Notes |
|---|---|
| `BindingOperations.EnableCollectionSynchronization(Files, lock)` | Lets a background thread modify a bound collection (with a lock). Legitimate and less code; it does **not** solve the flooding, and it moves the change notifications onto the worker |
| `IProgress<T>` / `Progress<T>` created on the UI thread | `Progress<T>` posts to the captured context automatically: elegant for the scalar, still needs batching |
| `DispatcherTimer` polling a shared state object | Simple and predictable: the UI reads the latest snapshot 20×/s. Very defensible, arguably simpler than the queue |
| `Dispatcher.BeginInvoke` per event | Fixes 1 and 2 but not 3: the dispatcher queue fills with thousands of items and the UI still stutters |
| Rx (`Observable.Sample`) | Tidy if Rx is already in the app |

Ask: "which of these would you pick for the real app, and why?" A `DispatcherTimer` snapshot is often the pragmatic winner.

## Common mistakes

- `Dispatcher.Invoke` instead of `BeginInvoke` (keeps symptom 2).
- Marshalling the file list but leaving progress unbatched (symptom 3).
- `lock` around `Files` without `EnableCollectionSynchronization` (still the wrong thread).
- `Application.Current.Dispatcher` (null outside a WPF app; the tests catch it).
- Losing the final update: the last events arrive after the last flush. The tests check the end state.
- Throttling by dropping events instead of coalescing them (files would go missing from the list).

## Follow-up questions

- "Where should the throttling live: the SDK or the app?" (The SDK can offer a `ProgressInterval` option, see 09-01; the app must still assume nothing.)
- "The user scrolls a 100,000-row list while it grows. What now?" (Virtualization, `ObservableCollection` is O(n) for some operations; consider a bounded "recent files" view.)
- "How does this look in MAUI?" (`MainThread.BeginInvokeOnMainThread`; same rules, different API. See 12-02.)
- "What if a progress event arrives after the view model is disposed?" (The handlers are detached; a queued flush must tolerate it.)

## Scoring notes

| Level | Indicators |
|---|---|
| Weak | Wraps everything in `Dispatcher.Invoke`; explains the exception but not the slowdown |
| Solid mid-level | Correct marshalling with `BeginInvoke`, own dispatcher, some batching, final state correct |
| Strong | Explains the scalar-vs-collection asymmetry, chooses a coalescing strategy deliberately, handles the final flush and disposal |
| Senior | Discusses where throttling belongs in an SDK's contract, virtualization, MAUI equivalence, and measuring UI cost |
