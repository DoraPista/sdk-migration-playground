# 16-03 Migrating on a Bad Network – Discussion Notes

**Type:** system design · **Time:** 20 min

## Hints

1. "One request in fifty fails. How many failures does this migration see in total?"
2. "Which failures are worth retrying, and which ones mean it may already have worked?"
3. "Classify by status code, resume by asking the server what it has, idempotency keys for the writes, jittered backoff with a budget, and an atomically written checkpoint."

The numbers in the scenario are there to be used. A candidate who says "one request in fifty fails, so a
1.2-million-request migration sees 24,000 failures — retries are not an edge case, they are the normal path"
has understood the exercise.

## 1. Failure classification

The core of a good answer. Something like:

| Failure | Retry? | How |
|---|---|---|
| Connect timeout / DNS / connection reset | Yes | Backoff with jitter; these are the VPN restart |
| Read timeout mid-body | Yes, **as a resume**, not a replay | The server may have received it all |
| 502 / 503 / 504 | Yes | Honour `Retry-After`; jittered backoff |
| 429 | Yes | `Retry-After` is authoritative; reduce concurrency, do not just wait |
| 408, `HttpRequestException` on send | Yes | |
| 401 | Once, after a token refresh; then fail | Single-flight refresh (06-01), otherwise the whole fleet refreshes at once |
| 403 / 404 / 400 / 413 / 422 | No | Retrying a permission or validation error wastes the budget and can lock the account |
| 409 | Depends: it usually means "already done" — reconcile, don't retry blindly | |

Plus: **full jitter**, a **per-operation attempt cap**, an **overall retry budget** (e.g. retries ≤ 10 % of
requests), and a **circuit breaker** so a dead network stops the fleet instead of hammering it.

The VPN restart at 17:00 is the retry-storm question (08-03): everything fails at once, everything retries
at once, and without jitter and a budget the recovery itself keeps the system down.

## 2. Not sending twice, and knowing what arrived

- **Chunked, resumable sessions**: the server tracks received ranges; the client asks *"what do you have?"*
  after any doubt, and continues from there (07-02).
- **Idempotency keys** derived from (migration, file, content hash) so a replayed request is recognised — not
  a random GUID per attempt, which defeats the purpose (07-03).
- **The lost response problem**: the request arrived, the response did not. This is the one to push on. The
  answer is that the client cannot know locally — it must ask the server, and the server must be able to
  answer ("I have bytes 0–17 MB of this file, and its hash is X"). *Server as source of truth.*
- **Verify by hash**, per chunk and per file, and record what the server reported rather than what the client
  believes it sent.

## 3. Surviving the lid, the crash and the reboot

- A checkpoint file, written **after** the server confirms, flushed and renamed atomically (write temp +
  `File.Replace`) so a crash mid-write cannot corrupt it (10-03).
- The checkpoint records intent as well as completion, so a crash *between* upload and record is
  recoverable by reconciliation instead of guesswork.
- On start: read the checkpoint, ask the server what it has, take the server's answer, continue.
- Nothing is assumed about clean shutdown. There may not be one.

## 4. What the consultant sees

- Bytes transferred and remaining, not just a percentage; the current file; the number of files done.
- **An honest "retrying, waiting 40 s (attempt 3 of 8)" instead of a frozen bar.** This is what stops the
  "is it stuck?" phone call, and candidates rarely mention it.
- A visible state: *running / waiting for network / paused / failed*, and the time of the last success.
- A log or report at the end listing exactly what did not make it and why.

## 5. When to stop

- Per-file: give up after N attempts, mark it failed-retryable, **continue with the rest**, and list it at
  the end. One bad file must not stop 1.2 million.
- Per-run: stop if the error budget is exhausted or nothing has succeeded for X minutes — and leave a
  resumable checkpoint, not a half-state.
- Permanent failures (403, 422, a file the OS will not open) are recorded and skipped, not retried.

## Follow-ups

- "The response was lost but the chunk arrived. How does the client find out?" (Ask the server.)
- "Everything fails at 17:00 and recovers at 17:01. What does your client do?" (Jitter, budget, breaker.)
- "The customer asks why it's slower than a file copy." (Round trips, verification, rate limits — and be honest.)
- "Would you use Polly?" (Fine — but ask what policies, in what order, with what jitter, and how they test it.)

## Level indicators

| Level | Indicators |
|---|---|
| Weak | "Retry three times"; no classification; assumes a failed request did not arrive |
| Solid mid-level | Classifies failures, honours `Retry-After`, resumable chunks, a checkpoint that survives a crash |
| Strong | Idempotency keys, server as source of truth for the lost response, jitter and budgets, per-file give-up that continues the run |
| Senior | Thinks about the storm on recovery, the operator/consultant experience, error budgets, and what the end-of-run report must contain |
