# 07-03 Duplicate Documents – Interviewer Notes

**Type:** debugging / design (idempotency) · **Time:** 25 min · **Solution code:** `code/src/DocumentUploader.cs`

## What is wrong

The code sends an `Idempotency-Key`, but it's `Guid.NewGuid()` **per attempt**. To the platform every retry is a brand-new
request. When the response is lost (the server stored the file; the client saw a connection reset) the retry stores it again.

Re-running after a crash duplicates for the same reason: nothing about the key survives the process.

It's a nice trap: the code *looks* idempotent. The candidate has to ask **what the key identifies**.

## Hints

1. "When the response is lost, what does the server have, and what does the client know?"
2. "What does the server compare the `Idempotency-Key` with? When is it different for the same document?"
3. "Derive the key from what makes the upload *the same upload*: migration, document identity and content."

## Intended solution

`key = H(migrationId, documentId, sha256(content))`, identical across retries and re-runs.

- Same name, different documents → different `DocumentId` → both kept.
- Edited document → different hash → uploaded as a new version.
- Also: only retry transient failures (not 4xx).

### Alternatives

| Approach | Trade-off |
|---|---|
| Random key **persisted** with the local checkpoint before the first attempt | Works across restarts if the checkpoint is durable; more moving parts |
| Reconcile instead: after an ambiguous failure, `GET /migrations/{id}/files?name=` and compare hashes | Works without server idempotency; racy with concurrent uploaders; name collisions need care |
| Server-side dedupe by content hash | Changes platform semantics (two identical files in different folders?) |
| Key = file name | Wrong: same-name documents collide |
| Key = DocumentId only | Wrong: edits are silently dropped (the server replays the old result) |

## The theory worth hearing

- Over an unreliable network you can't get *exactly-once delivery*. You get **at-least-once delivery + idempotent processing = effectively once**.
- Idempotency keys need a server-side retention window: how long does the platform remember keys? (A clarifying question.)
- A key reused with **different content** should be rejected by a good server (409/422), to protect against client bugs.

## Common mistakes

- Moving `Guid.NewGuid()` outside the loop: fixes retries, not re-runs after a crash.
- Using the key only on the retry.
- Retrying 400/422 responses.
- Deriving a key by string concatenation without separators.

## Follow-ups

- "What if the migration ID changes when the user re-runs?" (Then the key changes. The migration must be resumed, not recreated; see 10-03.)
- "How long must the platform keep keys?" (At least as long as the longest plausible retry and re-run window: days.)
- "How would you test this without the mock server's `ResponseLost` fault?"

## Scoring notes

| Level | Indicators |
|---|---|
| Weak | Removes retries; or dedupes by file name; can't explain lost responses |
| Solid mid-level | Stable derived key per document+content (+migration); explains why random keys don't work |
| Strong | Handles re-runs and edits; only retries transient errors; at-least-once + idempotency framing |
| Senior | Key retention, server-side key/content mismatch validation, reconcile-vs-key trade-offs, multi-client scenarios |
