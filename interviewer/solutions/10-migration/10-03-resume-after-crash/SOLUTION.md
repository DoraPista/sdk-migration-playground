# 10-03 The App Died at 63% – Interviewer Notes

**Type:** recovery / resumability · **Time:** 40 min · **Solution code:** `code/src/`
**Maps to:** special scenario **C (Migration Crash)**

## What is wrong

| # | Defect | Incident |
|---|---|---|
| 1 | `CreateMigrationAsync` is called on **every** run; the checkpoint's `MigrationId` is overwritten and never used | Incident 1: two migrations for one customer |
| 2 | A file is written to the checkpoint as `"Uploaded"` **before** the upload happens | Incident 2: a crash in that window makes the resume skip a file that never arrived. **Silent data loss** |
| 3 | The checkpoint is the only source of truth; the platform is never asked what it has | A missing or stale checkpoint means re-uploading everything, or worse, skipping |
| 4 | `File.WriteAllText` is not atomic: a crash mid-write leaves a truncated file, and `LoadAsync` then throws | Incident 3: unusable checkpoint, "fixed" by deleting it (and re-uploading everything) |
| 5 | `RemoteFileId` returned by the platform is thrown away | Nothing to reconcile with later |
| 6 | The checkpoint is saved once per file, synchronously, even for tiny files | Performance; discuss batching/debouncing |

## Hints

1. "The app is killed between the two lines that record and upload a file. What does the next run believe?"
2. "Who actually knows which files the platform has?"
3. "Record **after** the fact, reconcile with the platform on resume, and write the checkpoint so that a crash can never truncate it."

## Intended solution

1. **Continue, don't create.** Migration id from the checkpoint; if unknown, `FindOpenMigrationAsync`; only then create.
2. **Reconcile.** At the start of every run, ask the platform for the file ids it has. That set decides what to skip;
   the checkpoint is pruned to match, so a phantom entry cannot survive.
3. **Upload first, record after** (with the remote file id). The worst case becomes *one repeated upload*, which the
   platform de-duplicates (07-03), instead of a lost file.
4. **Atomic checkpoint writes**: temp file + flush + `File.Move(..., overwrite: true)`; tolerate an unreadable checkpoint by falling back to the platform.

### The reasoning to listen for

> "A crash can happen between any two operations. Which order makes the worst case survivable?"
> Recording after the upload can cost a duplicate attempt; recording before can cost a file. Duplicates are recoverable, missing files are not.

That is the heart of this exercise: **at-least-once + idempotency beats at-most-once** for data you cannot recreate.

### Alternatives

- Checkpoint per file as an append-only log (one line per uploaded file): crash-safe by construction, no rewrite of the whole file; needs compaction.
- No local checkpoint at all: reconcile with the platform every time (simplest; costs one listing call per run, and a listing may be big for 100k files, and it can't record local-only state like "hashed").
- Store the checkpoint next to the source data rather than in app data, so it travels with the export (interesting for consultants moving between machines).
- Write the checkpoint every N files or every N seconds instead of per file, accepting a bounded amount of repeated work.

## Common mistakes

- Reconciling **only** when the checkpoint is missing (incident 2 survives).
- Trusting checkpoint entries in addition to the platform list ("union"), which re-introduces the phantom-file bug.
- Catching `JsonException` and deleting the checkpoint, then re-uploading everything (that is what the customer did by hand).
- `File.Replace` without a backup on a network share, or `File.Delete` + `File.Move` (a crash between them loses both).
- Saving the checkpoint after the loop (one crash loses everything).
- Forgetting that `ListUploadedFileIdsAsync` may be paginated in reality; ask about 100,000 files.

## Follow-up questions

- "The upload succeeded but the response was lost. What does your resume do?" (Reconciliation finds it; see 10-04 and 07-03.)
- "What if the user edits a file between runs?" (Content hash in the checkpoint; re-upload changed files. What does the platform do with a second version?)
- "Two laptops resume the same customer's migration." (Ownership/lease on the platform side; last-writer-wins is not good enough.)
- "How would you test that the checkpoint is really crash-safe?" (Kill the process for real at random points and re-run; the tests here approximate it with injected crashes.)
- `shared/MockData/migration-state.json` is a 63% checkpoint with one file `InProgress` and an `uploadId`: "what would you do with that entry?" (Resumable upload session; see 07-02.)

## Scoring notes

| Level | Indicators |
|---|---|
| Weak | Keeps creating migrations; moves the record after the upload but never reconciles; deletes corrupt checkpoints |
| Solid mid-level | All four defects fixed; explains the ordering argument; atomic write |
| Strong | Treats the platform as the source of truth and prunes the checkpoint; tolerant loading; keeps remote ids; discusses duplicate-vs-missing trade-off explicitly |
| Senior | Append-only or debounced checkpointing, pagination and scale, multi-device ownership, and how to verify crash safety for real |
