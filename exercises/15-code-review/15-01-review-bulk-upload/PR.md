# PR #482 – Bulk upload for migrations

**Author:** @sam · **Branch:** `feature/bulk-upload` → `main` · **Files changed:** 2 · **+168 −0**

## What this does

Customers keep asking to send a whole project in one go instead of file by file, so this adds
`BulkUploader`. `MigrationWindow` calls `UploadProject(...)` and the files go up in parallel, which is
much faster than the loop we had before — a 12-file project went from 9 s to 2 s on my machine.

## Notes for the reviewer

- I create the `HttpClient` in a `using` per call so we don't leak connections.
- Uploads run in parallel with `Task.WhenAll`, that's what made it fast.
- I kept the token in a static field so we only sign in once per process.
- I added a retry loop because the test server sometimes returns 500.
- Logging is a bit chatty but it has helped me a lot while debugging — happy to trim it later.
- No unit tests yet: it is mostly plumbing and hard to test because of the HTTP calls. I tested it by hand
  against the mock server with the `northwind` dataset.

## Files

- `src/BulkUploader.cs` (new)
- `src/UploadResult.cs` (new)
