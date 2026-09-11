# Exercise 02-01 – The Migration Finishes Too Early

Difficulty: Medium
Estimated Time: 20 minutes

## Skills

- async control flow
- debugging production symptoms
- error propagation

## Scenario

Three reports from the field, all in the same week:

1. "The app says *Migration complete* but the portal shows 1,187 of 1,200 files. After a few minutes the
   missing files appear."
2. "The portal shows the migration as *Completed* and then files keep arriving." The platform team says
   files arriving after completion get quarantined.
3. "One of our shares was offline. The app said the migration succeeded, and the summary said
   0 failed files. We lost the files from that share."

`MigrationRunner` is the component that uploads files in parallel and then marks the migration as
complete on the platform.

## Your Task

Find the cause of each symptom and fix `MigrationRunner`.

## Constraints

- Keep uploading in parallel (`workerCount`).
- Keep the public API of `MigrationRunner`.

## Acceptance Criteria

- `RunAsync` returns only after every upload has finished, one way or another.
- The platform is told the migration is complete only if every file was uploaded.
- The summary reports failed files accurately.

## How to Run

```bash
dotnet test exercises/02-debugging/02-01-migration-finishes-too-early/tests
```

## When You're Done

All tests pass, and you can explain each symptom from the customer reports.
