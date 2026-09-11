# 02-02 The Failed Migration That Reported Success – Interviewer Notes

**Type:** debugging / error-handling strategy · **Time:** 20 min · **Solution code:** `code/src/`

## What is wrong

| Location | Defect | Effect |
|---|---|---|
| `SourceScanner.Scan` | `catch (Exception) { return empty; }` | An unreachable share looks like an empty folder, so the run "succeeds" with 0 files (the headline incident) |
| `UploadManifestSafeAsync` | Logs a warning and continues | Files are uploaded against a migration the platform never accepted |
| File loop | `catch (Exception)` + warning | Failed files vanish from the result; **cancellation is swallowed** per file, so the run continues |
| End of run | Always `CompleteAsync` and `Succeeded` | Status doesn't reflect reality |
| Logging | `LogWarning("...{Message}", ex.Message)` drops the exception | Stack traces are lost for support |

The theme: **"catch and continue" without a policy.** Every catch needs an answer to "is this fatal, per-item, or a bug?"

## Hints

1. "What does `Scan` return when the drive is disconnected? How would the caller know?"
2. "Go through each `catch`: what is it protecting against, and what does it hide?"
3. "Fatal problems (source, manifest) should end the run with `Failed`. Per-file problems should be recorded. Cancellation should propagate."

## Intended solution

- Scanner throws when the **root** can't be read; inaccessible sub-folders are reported (not silently skipped).
- Manifest failure → `Failed` result before any upload.
- Per-file catch only for expected I/O and HTTP failures; record `FileFailure`; continue.
- `OperationCanceledException` is not caught (the test expects it to propagate; returning a `Cancelled` status is equally fine if the enum is extended).
- `PartiallySucceeded` + **no** `CompleteAsync` when anything failed.
- Log exceptions as exceptions (`LogWarning(ex, ...)`).

### Alternatives

- Return `Failed` with the exception attached instead of catching at the scanner level. Fine.
- Extend `MigrationStatus` with `Cancelled` rather than throwing. Also fine; ask how the UI would use it.
- A retry for transient HTTP failures per file. Good instinct, out of scope here (see 08-01).

## Common mistakes

- Removing *all* try/catch blocks so the first locked file aborts the whole migration (fails the per-file test).
- `catch (Exception ex) when (ex is not OperationCanceledException)`: OK, but it still treats `NullReferenceException` (a bug) as a file failure. Discuss.
- Returning `Failed` for a legitimately empty folder. Ask them: is an empty source an error? (A great clarifying question. A warning is probably best.)
- Treating the manifest failure as per-file.

## Clarifying questions to reward

- Is an empty source folder valid?
- Should the migration be completed on the platform with some files missing, or left open for a re-run?
- Is there a retry policy elsewhere?
- What does the user see for `PartiallySucceeded`, and can they retry only the failed files?

## Follow-ups

- "Which of these errors would you want in telemetry, and at what level?"
- "A file is deleted between scan and upload. Per-file failure, or ignore?"
- "How would you test the locked-file case on Linux CI?" (FileShare semantics differ; use an abstraction or an OS-conditional test.)

## Scoring notes

| Level | Indicators |
|---|---|
| Weak | Deletes catch blocks wholesale, or adds more catch-and-log; misses the scanner as the root cause |
| Solid mid-level | Fatal vs per-file separation; correct statuses; cancellation propagates; complete only on full success |
| Strong | Narrows exception filters to expected types; keeps exceptions in logs; handles inaccessible sub-folders explicitly |
| Senior | Frames an error-handling policy for the SDK (fatal / per-item / bug), talks about user-facing messages vs diagnostics, and platform-side guarantees (completion validation) |
