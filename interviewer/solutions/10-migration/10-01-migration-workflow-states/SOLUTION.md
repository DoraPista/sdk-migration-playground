# 10-01 A Migration That Says "Completed" – Interviewer Notes

**Type:** debugging / workflow design · **Time:** 30 min · **Solution code:** `code/src/MigrationWorkflow.cs`

## What is wrong

| # | Defect | Incident |
|---|---|---|
| 1 | Blocking validation issues are **logged and ignored** | Incomplete archive sealed as Completed (incident 1). The whole point of pre-flight validation is lost |
| 2 | Per-file failures are collected but never checked: `Verify` and `Complete` run anyway | Same incident: the platform is told "complete" for an archive with missing files |
| 3 | `VerifyAsync`'s result is assigned to `verified` and **never used** | A failed verification completes the migration |
| 4 | `CompleteAsync` is wrapped in its own `try/catch` that swallows the failure, then `State = Completed` | Incident 2: the app says Completed, the platform says Uploading |
| 5 | `catch (Exception)` turns cancellation into `Failed` | Incident 3 |
| 6 | Nothing prevents a second `RunAsync` | Stage list grows, a second migration is created on the platform (see 04-02) |
| 7 | Cancellation is only observed inside the platform calls; the loop doesn't check between files | A cancel during a long file list keeps uploading |

The theme: **the workflow reports the state it hoped for, not the state it achieved.**

## Hints

1. "Follow what happens to the result of each stage: validation, uploads, verification, completion."
2. "Which of these stages can fail without anyone noticing?"
3. "Decide, per stage: does a failure stop the workflow, get recorded, or get ignored? Then make the state reflect what the platform actually did."

## Intended solution

- Blocking issues → `Failed` **before** `CreateMigration`.
- Failed files → `Failed`, `Complete` never called; the migration stays open so it can be resumed (10-03).
- `Verify` result checked; `Complete` failure propagates (no swallow).
- Cancellation caught separately (`when (cancellationToken.IsCancellationRequested)`) → `Cancelled`.
- Guard against re-running; `ThrowIfCancellationRequested` between files.

### Alternatives

- A **state machine** with an explicit `Transition(from → to)` that throws on illegal transitions. More machinery, and a good answer to "how would you keep this correct as stages are added" (10-02 goes further).
- Stages as objects (`IMigrationStage`) run by a driver, with a result type per stage. Also good; the driver then owns the failure policy.
- Returning `PartiallySucceeded` instead of `Failed` when some files uploaded. Perfectly defensible: the key is that **the platform is not told "complete"**. Ask them to justify what the user sees.

## Common mistakes

- Throwing on a failed file, so the remaining files are skipped (the test checks the others still upload).
- Catching `OperationCanceledException` in the per-file `catch` and recording the file as failed.
- Setting `State = Completed` before awaiting `CompleteAsync` ("it's about to be").
- Using `Complete` as "we're done here" rather than a platform command with consequences.

## Follow-up questions

- "What should happen to a migration that ends in `Failed` with 2 of 40 files missing?" (Leave it open; resume; see 10-03. What does the *user* see?)
- "Who decides that `Complete` is irreversible: us or the platform?" (The platform. That's why the client must be careful, and why a server-side check of manifest vs received files is a good safety net.)
- "Draw the state machine." (Look for: terminal states, no transition out of `Completed`, `Cancelled` only from running states, and what happens to `Verifying` → `Failed`.)
- "How would you test that `Complete` is never called after a failure, for every possible stage failure?" (Property-style or matrix tests over failure injection points.)

## Scoring notes

| Level | Indicators |
|---|---|
| Weak | Fixes one incident; still completes when files failed; doesn't notice the ignored `verified` variable |
| Solid mid-level | All four defects; cancellation separated; re-run guarded; explains why completion must be last |
| Strong | Discusses `PartiallySucceeded` vs `Failed`, resumability, and a state machine that makes illegal transitions impossible |
| Senior | Adds platform-side safeguards, operational view (what support sees), and how to keep the workflow honest as stages are added |
