# Exercise 03-01 – Five Hundred Files at Once

Difficulty: Medium
Estimated Time: 25 minutes

## Skills

- concurrency with async I/O
- throttling
- cancellation
- progress reporting

## Scenario

A customer with a typical project folder (≈ 500 drawings and photos) reports that the migration:

- makes their corporate proxy reset connections ("too many simultaneous connections from one client"),
- makes the desktop app's memory jump by several hundred MB at the start of the upload,
- takes a long time to react when they cancel.

`UploadOptions.MaxConcurrentUploads` exists. The support engineer set it to 4 for this customer.
Nothing changed.

## Your Task

Fix `BatchUploader` so it respects the configured limit, while still uploading in parallel.

## Constraints

- Keep the public API of `BatchUploader`.
- Progress must still report how many files have completed.

## Acceptance Criteria

- Never more than `MaxConcurrentUploads` uploads in flight at the same time.
- Uploads do run in parallel when the limit allows it.
- After cancellation, no new uploads start.

## How to Run

```bash
dotnet test exercises/03-async/03-01-five-hundred-files/tests
```

## When You're Done

All tests pass, and you can explain what the current code does with 500 files, step by step.
