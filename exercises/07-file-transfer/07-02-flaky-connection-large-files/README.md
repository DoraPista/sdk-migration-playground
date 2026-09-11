# Exercise 07-02 – Large Files on a Flaky Connection

Difficulty: Hard
Estimated Time: 40 minutes

## Skills

- resumable transfers
- reasoning about what the server already has
- retry policies based on progress
- failure recovery

## Scenario

A heritage trust migrates laser scans (1–40 GB each) from a site office connected over a 4G router.
The connection drops every few minutes.

The SDK already uses the platform's **resumable upload** API (see *Resumable uploads* in
`shared/MockServer/API.md`), yet:

- Large files never finish. The log shows the upload restarting from 0% after every drop:
  *attempt 1… 61%… connection reset… attempt 2… 58%… connection reset… attempt 3… gave up*.
- The platform team says the trust has **hundreds of abandoned upload sessions**, and the storage
  they hold is billed.
- After a platform maintenance window, some uploads failed with `404 Upload session not found` and never
  recovered.

## Your Task

Make `ResumableUploader` actually resume.

Ask whatever clarifying questions you think you need.

## Constraints

- Keep the public API. `MaxAttemptsWithoutProgress` means what it says.
- Files can be larger than memory.

## Acceptance Criteria

- An upload interrupted repeatedly completes, as long as each attempt makes progress.
- Bytes the server already has are not sent again, and one file uses one upload session.
- An upload whose session was lost starts a new one.
- If attempts stop making progress, the uploader gives up.
- Empty files work too.

## How to Run

```bash
dotnet test exercises/07-file-transfer/07-02-flaky-connection-large-files/tests
```

## When You're Done

All tests pass, and you can explain how your uploader knows where to continue from, and why it trusts that source.
