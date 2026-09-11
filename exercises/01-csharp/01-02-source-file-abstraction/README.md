# Exercise 01-02 – Where Do Files Come From?

Difficulty: Medium
Estimated Time: 25 minutes

## Skills

- abstraction and API design
- streams and resource ownership
- designing for callers you don't control

## Scenario

The SDK's upload API takes file **paths**:

```csharp
await uploadService.UploadAsync(migrationId, @"C:\Projects\Harbour\spec.pdf");
```

That was fine while the only customer was the WPF app. Now:

- The **MAUI** app gets files from the platform file picker. On Android that is a content URI with no
  real file-system path. All you get is something you can open a stream from. The length may not be known up front.
- The **WPF** app wants to migrate reports it generates in memory, without writing temp files first.
- The **archive importer** team wants to upload entries straight out of a `.zip` without extracting 30 GB to disk.
- The desktop team reports that `MigrationManifestBuilder` keeps file handles open for thousands of files
  while a migration is queued. *(Check whether that is true of this code, or of how it will be used.)*

## Your Task

Design a representation of "a file the SDK can migrate" that doesn't tie the SDK to a particular UI or
file system. Change `UploadService` and `MigrationManifestBuilder` to use it, and show that at least
two different kinds of source work.

Ask whatever clarifying questions you think you need.

## Constraints

- The SDK has to compute a SHA-256 before it uploads, and it may need to re-send a file after a transient failure.
- The SDK must not depend on WPF, MAUI or `System.IO.Compression`-specific types in its public API.
- You may change the existing tests to fit your API, but keep what they verify.

## Acceptance Criteria

- Existing behaviour for local files still works.
- A second kind of source (for example in-memory content, or a stream that can only be opened, not seeked)
  can be uploaded and put in a manifest.
- It is clear who opens and who disposes streams.

## How to Run

```bash
dotnet test exercises/01-csharp/01-02-source-file-abstraction/tests
```

## When You're Done

Be ready to explain the trade-offs in your design, and what you deliberately left out.
