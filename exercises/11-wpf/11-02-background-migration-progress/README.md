# Exercise 11-02 – Progress From a Background Migration

Difficulty: Medium
Estimated Time: 25 minutes

## Skills

- UI-thread rules in WPF
- marshalling events from worker threads
- keeping the UI (and the worker) responsive

## Scenario

The migration runs on a background thread in the SDK and raises two events: one per uploaded file, and one
for overall progress (often, every few hundred KB).

The desktop app's `MigrationViewModel` subscribes to both. Since the last release:

1. Starting a migration crashes the app:
   `NotSupportedException: This type of CollectionView does not support changes to its SourceCollection from a
   thread different from the Dispatcher thread.`
2. Before that release, the same migration took **11 minutes**; when the file list was hidden it took **4**.
   Someone "fixed" it by marshalling every event, and now a migration takes even longer while the window is busy.
3. During a migration the app is sluggish: the progress bar repaints constantly and the rest of the UI stutters.

The SDK's events are raised on a worker thread, one per file and many per second. That is not going to change:
an SDK cannot know what a UI thread is.

## Your Task

Make the view model handle the SDK's events correctly.

## Constraints

- Keep the public API of `MigrationViewModel` (`Files`, `Percent`, `Status`, the constructor).
- The SDK must not be slowed down by the UI: raising an event must not wait for the window.
- The UI must not be updated more than about 20 times a second, however fast the events arrive.

## Acceptance Criteria

- Files appear in the bound list while the migration runs, with no exception.
- The migration finishes even while the UI thread is busy.
- Thousands of progress events cause few UI updates.
- When the migration ends, the list and the percentage are correct.

## How to Run

```bash
dotnet test exercises/11-wpf/11-02-background-migration-progress/tests
```

Windows only: the tests use a real dispatcher and real bindings.

## When You're Done

All tests pass, and you can explain which of WPF's rules applies to scalar properties and which to collections.
