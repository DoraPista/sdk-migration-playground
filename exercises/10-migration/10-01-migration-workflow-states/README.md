# Exercise 10-01 – A Migration That Says "Completed"

Difficulty: Hard
Estimated Time: 30 minutes

## Skills

- workflow and state machines
- failure handling across stages
- honest reporting to the platform and the user

## Scenario

`MigrationWorkflow` runs the stages of a migration: authenticate → provision → validate → create →
upload files → verify → complete.

Three incidents in the last month:

1. A customer's export had 40 files listed in the manifest that don't exist. Pre-flight validation
   reported them, the migration ran anyway, and the platform now holds an incomplete archive that is
   marked **Completed**. Nobody noticed for three weeks.
2. A migration reported **Completed** in the app while the platform still shows it as *Uploading*.
   The last call to the platform had failed.
3. A user pressed Cancel. The app showed **Migration failed** and a support ticket was raised.

The platform treats *Completed* as final: after that, no more files can be added, and the customer's
source system is usually decommissioned soon after.

## Your Task

Fix `MigrationWorkflow` so the state it reports is the truth.

## Constraints

- Keep the public API (`RunAsync`, `State`, `WorkflowResult`).
- Don't change the order of the stages.
- Per-file upload failures must not abort the remaining files; they must, however, prevent completion.

## Acceptance Criteria

- Blocking validation issues stop the migration before anything is created on the platform.
- A migration with failed files or a failed verification is never completed on the platform, and is never reported as completed.
- Cancellation is reported as cancellation.
- A finished workflow can't be run a second time.

## How to Run

```bash
dotnet test exercises/10-migration/10-01-migration-workflow-states/tests
```

## When You're Done

All tests pass, and you can draw the state machine, including which transitions are not allowed.
