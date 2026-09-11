# Exercise 10-03 – The App Died at 63%

Difficulty: Hard
Estimated Time: 40 minutes

## Skills

- resumability and checkpoints
- deciding what is the source of truth
- durable writes
- recovering from your own corrupt state

## Scenario

Migrations run for hours. Laptops get closed, Windows installs updates, and the app crashes.
`ResumableMigration` keeps a local checkpoint so that the next run continues where the last one stopped.

From the field:

1. A customer's laptop rebooted at 63%. They restarted the migration. The portal now shows **two** migrations
   for that customer: the first with 1,180 files, the second with 690. Support had to merge them by hand.
2. Worse: a file that was "already uploaded" according to the app is **not** in the portal at all.
   The customer's source system was decommissioned two weeks later.
3. One customer can't resume at all: every run fails with
   `JsonException: Expected depth to be zero at the end of the JSON payload`. Their checkpoint file
   ends mid-word. Deleting it "fixes" it, and re-uploads everything.

The platform can tell you what it already has: `FindOpenMigrationAsync` and `ListUploadedFileIdsAsync`.

## Your Task

Make a resumed migration continue the migration that was interrupted, upload exactly what is missing,
and survive a checkpoint that was written during a crash.

Ask whatever clarifying questions you think you need.

## Constraints

- Keep the public API (`ResumableMigration.RunAsync`, `MigrationSummary`, `ICheckpointStore`).
- Uploading a file twice is not acceptable; skipping a file is much worse.
- The app can be killed at any moment, including in the middle of writing the checkpoint.

## Acceptance Criteria

- A resumed migration continues the same platform migration.
- Every file ends up on the platform exactly once, whatever moment the crash happened at.
- A corrupt or missing checkpoint does not start a second migration and does not re-upload everything.

## How to Run

```bash
dotnet test exercises/10-migration/10-03-resume-after-crash/tests
```

`shared/MockData/migration-state.json` is a real checkpoint from a migration that was interrupted at 63%,
and `shared/MockData/datasets/malformed/truncated-state.json` is one that was interrupted while being written.

## When You're Done

All tests pass, and you can explain what you treat as the source of truth, and why.
