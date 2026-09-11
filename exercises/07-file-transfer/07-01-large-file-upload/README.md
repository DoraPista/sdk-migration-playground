# Exercise 07-01 – The 100 GB File

Difficulty: Medium
Estimated Time: 25 minutes

## Skills

- streams and memory
- `HttpContent`
- progress reporting
- measuring instead of guessing

## Scenario

Surveying customers migrate point-cloud scans. These are single files of 5–120 GB.

- Uploading a 100 GB scan fails immediately with
  `IOException: The file is too long. This operation is currently limited to supporting files less than 2 gigabytes in size.`
- A 1.8 GB file works on the developer's workstation. On the customer's 8 GB laptops the app's memory climbs
  to ~4 GB and Windows starts paging. Sometimes it crashes with `OutOfMemoryException`.
- While a file uploads, the progress bar sits at 0% for twenty minutes and then jumps to 100%.

The server contract (`shared/MockServer/API.md`) requires the SHA-256 of the file **in a header**, i.e. before the body is sent.

## Your Task

Fix `LargeFileUploader` so it can upload files of any size with modest, constant memory use and
meaningful progress.

## Constraints

- Keep the public API.
- The `Content-Length` must be declared (the platform's gateway rejects chunked uploads).

## Acceptance Criteria

- The upload arrives intact, with a correct hash header.
- Memory use does not grow with the file size.
- Progress is reported while the file uploads, not only at the end.

## How to Run

```bash
dotnet test exercises/07-file-transfer/07-01-large-file-upload/tests
```

The memory test creates a 128 MB file in your temp folder and deletes it afterwards.

## When You're Done

All tests pass, and you can answer: where was the memory going, how would you have measured it, and what does your fix cost?
