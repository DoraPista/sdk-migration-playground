# Exercise 04-01 – The Checkpoint Drifts

Difficulty: Hard
Estimated Time: 30 minutes

## Skills

- race conditions
- read-modify-write across `await`
- designing thread-safe state
- deterministic concurrency testing

## Scenario

Upload workers call `MigrationStateTracker.MarkUploadedAsync` after each file. The tracker persists the list
of uploaded files so that an interrupted migration can be resumed.

The unit tests are green and have been for months. In production:

- After a crash, the resumed migration re-uploads files that the platform already has.
- The checkpoint file sometimes says 3 files were uploaded when the platform has 40.
- The progress bar sometimes jumps **backwards**.

It happens more with customers who use higher `MaxConcurrentUploads` settings.

## Your Task

Explain what is happening and fix `MigrationStateTracker` so that concurrent updates are never lost.

## Constraints

- Several workers (up to 16) call `MarkUploadedAsync` concurrently.
- `IStateStore` is slow (it writes to disk, and on some customer machines to a network share). Its interface can't change.
- Keep the public API of `MigrationStateTracker`.

## Acceptance Criteria

- No update is ever lost, whatever the concurrency.
- Progress notifications never go backwards.
- A file reported twice is only counted once.

## How to Run

```bash
dotnet test exercises/04-concurrency/04-01-migration-state-drift/tests
```

## When You're Done

All tests pass, and you can explain why the original tests never caught this.
