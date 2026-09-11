# Exercise 02-03 – Source Files Stay Locked

Difficulty: Medium
Estimated Time: 25 minutes

## Skills

- resource management and disposal
- event subscriptions and object lifetime
- reading code for failure paths

## Scenario

Two tickets that support thinks are unrelated:

1. **"Can't save my drawing."** After a migration had network problems, engineers can't save or rename some
   of their CAD files ("The file is in use by another process"). It goes away when the desktop app is restarted.
2. **"The app gets slower every day."** The desktop app stays open for days. Each migration opens a
   `MigrationSession`. Memory keeps growing. When the office Wi-Fi comes back after a drop,
   the platform sees a burst of uploads, some of them for migrations that finished days ago.

`NetworkMonitor` is a single app-wide instance that tells interested parties when connectivity changes.

## Your Task

Find and fix the causes of both tickets.

## Constraints

- Keep the public API of `FileUploader` and `MigrationSession`.
- Sessions must still retry their pending uploads when connectivity comes back while they are active.

## Acceptance Criteria

- After an upload attempt, successful or not, the source file is no longer held open.
- A disposed session no longer reacts to connectivity changes and can be garbage-collected.

## How to Run

```bash
dotnet test exercises/02-debugging/02-03-source-files-stay-locked/tests
```

## When You're Done

All tests pass, and you can explain why ticket 1 went away when the app was restarted.
