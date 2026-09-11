# Exercise 04-03 – The Pipeline That Hangs

Difficulty: Hard
Estimated Time: 30 minutes

## Skills

- producer/consumer
- backpressure and bounded memory
- error and cancellation propagation across tasks

## Scenario

To start uploading before a slow network share has been fully enumerated, the migration uses a small
pipeline: one producer scans the share, several workers upload what it finds.

It's fast, and memory stays flat even for shares with millions of files. But:

1. When the scanner hits a folder it can't read (`IOException` on a flaky share), the app sits at
   "Uploading…" **forever**. No error, no progress.
2. When the user cancels while the scan of a huge share is still running, the app hangs the same way.
3. When the platform starts rejecting uploads (for example, the customer's subscription expired), the app
   keeps scanning and throwing errors for the whole share, which takes hours. Product wants the migration
   to stop at the first upload failure. It will be retried later as a whole.

## Your Task

Fix `ScanAndUploadPipeline` so that it always finishes, and finishes for the right reason.

## Constraints

- Keep the public API.
- The scan must overlap with the uploads (don't scan everything first).
- Memory must stay bounded: the scanner must not run arbitrarily far ahead of the uploads.

## Acceptance Criteria

- A scanner failure ends the run promptly with the scanner's error.
- Cancellation ends the run promptly, even while the scanner is still working.
- An upload failure ends the run promptly with the upload's error, and scanning stops.
- Everything is still uploaded when nothing goes wrong.

## How to Run

```bash
dotnet test exercises/04-concurrency/04-03-scan-and-upload-pipeline/tests
```

## When You're Done

All tests pass, and you can explain, for each failure, which task was waiting for what.
