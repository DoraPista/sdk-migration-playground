# Exercise 02-02 – The Failed Migration That Reported Success

Difficulty: Medium
Estimated Time: 20 minutes

## Skills

- exception handling strategy
- fatal vs recoverable errors
- honest result reporting

## Scenario

A customer ran a migration from a mapped network drive that had silently disconnected.
The desktop app showed:

> ✔ Migration completed successfully. 0 files uploaded.

The customer decommissioned the old file server the next day.

While investigating, support found more:

- If the platform rejects the migration manifest, the app carries on uploading and still reports success.
- A file that was locked by another program was skipped. The summary didn't mention it.
- When a user cancels, the app keeps going and eventually reports success.

## Your Task

Make `MigrationService` report what actually happened.

## Constraints

- `MigrationResult` and `MigrationStatus` are what the UI binds to. You may add to them, but don't remove anything.
- Individual unreadable files must not stop the rest of the migration.
- Don't mark the migration complete on the platform unless every file made it.

## Acceptance Criteria

- An unreachable source folder fails the migration, with an error that says which folder.
- A rejected manifest fails the migration before any file is uploaded.
- Unreadable files are listed in the result; the other files are still uploaded.
- Cancellation is reported as cancellation.

## How to Run

```bash
dotnet test exercises/02-debugging/02-02-failed-migration-reports-success/tests
```

## When You're Done

All tests pass, and you can say which failures you treat as fatal, which you treat as per-file, and why.
