# 07-02 Large Files on a Flaky Connection – Interviewer Notes

**Type:** implementation / reliability · **Time:** 40 min · **Solution code:** `code/src/ResumableUploader.cs`

## What is wrong

| Symptom | Cause |
|---|---|
| Restarts from 0% after every drop | Each attempt creates a **new session** and PUTs from offset 0. The resumable API is used, but nothing is resumed |
| Hundreds of abandoned sessions | One new session per attempt, never completed or aborted |
| "Gave up" although each attempt got to ~60% | `MaxAttemptsWithoutProgress` is used as **total attempts**, so attempts that make progress count against the limit |
| 404 after maintenance never recovered | Only `HttpRequestException` is retried, and each retry starts a fresh session. That one accidentally works; the real problem is that nothing *checks* the session |
| Empty files | `ContentRangeHeaderValue(0, -1, 0)` throws `ArgumentOutOfRangeException` |
| (Latent) | Only `HttpRequestException` is considered transient (not `IOException`, timeouts, 5xx vs 4xx) |

## Hints

1. "After a drop at 61%, how much of the file does the server have? How could the client find out?"
2. "Which information do you trust after a failure: what you think you sent, or what the server says it has?"
3. "Keep one session; after a failure `GET /uploads/{id}`; continue from `received`; count attempts that make no progress; if the session is gone, start a new one."

## Intended solution

- One session per file. Resume offset = the **server's** `received` (after a failure the client can't know how much of what it wrote arrived).
- `PUT` from the offset to the end (or in chunks), with `Content-Range`; handle `409` by adopting the server's offset.
- Progress-based give-up: reset the counter whenever `received` grows.
- `404` → new session; empty files → complete directly.

### Alternatives

- **Fixed-size chunks** (e.g. 8 MB PUTs) instead of "the rest of the file": smaller retransmission after a drop, clearer progress, and natural checkpoints. It costs more requests. Both are acceptable; chunks are the more robust industry pattern (Azure Blob block upload, tus, S3 multipart).
- Parallel chunk upload (block blobs): faster on high-latency links; needs a commit step with the block list.
- Persisting `uploadId` + offset locally so a **process restart** can resume too (10-03 connects to this).

## Common mistakes

- Resuming from the client's own byte count instead of asking the server (after a drop, bytes in socket buffers were never received).
- Retrying the same `StreamContent` without re-seeking (sends from the wrong position, or throws).
- Still counting total attempts ("progress" not defined).
- Treating `404` on `GET /uploads` as fatal.
- Abandoning sessions on permanent failure without telling the server (a DELETE endpoint would be nice; ask whether they'd want one).

## Clarifying questions worth rewarding

- Do sessions expire? How long? (Decides whether local persistence of `uploadId` is useful.)
- Can the server verify partial content (per-chunk hashes)?
- What's the maximum chunk size the gateway accepts? Is there a per-request timeout at the proxy?
- Is there a cost to abandoned sessions (there is, per the scenario)?

## Follow-ups

- "The app is closed at 70% of a 40 GB file. What happens next launch?" (Resume needs the `uploadId` persisted and a session TTL long enough.)
- "How do you know the final file is correct?" (Server compares the full SHA-256 on complete; a mismatch resets the session, so the file is re-sent.)
- "The 4G link is metered. What would the customer want from the SDK?" (Bandwidth limits and pause/resume.)

## Scoring notes

| Level | Indicators |
|---|---|
| Weak | Increases MaxAttempts; keeps restarting from 0; doesn't use `GET /uploads/{id}` |
| Solid mid-level | Single session, resume from server's offset, progress-based give-up, 404 → new session |
| Strong | Handles 409 offset correction, empty files, a transient-error classification; explains why the server is the source of truth |
| Senior | Chunking strategy, session TTL and local persistence, cost of abandoned sessions, parallel blocks, bandwidth policy |
