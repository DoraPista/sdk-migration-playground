# Exercise 02-06 – The Server Rejects Every File

Difficulty: Medium
Estimated Time: 20 minutes

## Skills

- streams
- HTTP request lifecycle
- reading server error responses

## Scenario

A new upload component went through code review and was deployed to the pilot customers. Every single upload
now fails with `422 Unprocessable Entity – Checksum mismatch`.

Oddly, one pilot customer says their **empty** files (0 bytes) migrate fine.

After a quick "fix" someone also noticed that uploads that hit a temporary `503` never recover.

The server contract is in `shared/MockServer/API.md` (`POST /migrations/{id}/files`).

## Your Task

Find out why the server rejects the files, and why the retry doesn't recover. Fix `ChecksumUploader`.

## Constraints

- The server requires the SHA-256 in the `X-Content-SHA256` header, so it has to be known before the body is sent.
- Files can be large. Don't load them into memory.

## Acceptance Criteria

- Files arrive on the server byte-for-byte intact.
- An upload that gets a transient `503` succeeds on a later attempt.

## How to Run

```bash
dotnet test exercises/02-debugging/02-06-server-rejects-every-file/tests
```

The tests run against the local mock server in-process. Nothing needs to be started by hand.

## When You're Done

All tests pass, and you can explain why empty files worked.
