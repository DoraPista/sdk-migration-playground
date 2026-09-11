# Exercise 03-03 – One Failure Hides the Others

Difficulty: Medium
Estimated Time: 20 minutes

## Skills

- `Task.WhenAll` and exception propagation
- partial failure
- designing result types

## Scenario

The desktop app uploads metadata files in small batches. When a batch has problems, the app shows
the failed files and offers **Retry failed**.

Reports:

- "Retry failed" re-uploads **every** file in the batch. The platform now shows duplicates of files that were
  fine the first time.
- A batch had one server error and one file rejected by validation. The app only showed the server error,
  and support spent a day on the wrong problem.

## Your Task

Make `BatchUploader` report an accurate outcome for every file in the batch.

## Constraints

- Keep the public API (`BatchUploader`, `BatchResult`, `FileOutcome`).
- Files in a batch are uploaded concurrently.
- Cancellation is not a failure of any particular file.

## Acceptance Criteria

- Each file's outcome is correct, and each failure keeps its own error.
- "Retry failed" resends only the files that failed.
- The batch finishes only when every upload has finished.

## How to Run

```bash
dotnet test exercises/03-async/03-03-batch-upload-errors/tests
```

## When You're Done

All tests pass, and you can explain what `await Task.WhenAll(...)` does with multiple exceptions.
