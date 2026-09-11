# 10-04 The Upload That Might Have Worked – Interviewer Notes

**Type:** expert discussion · **Time:** 20 min · **Demo:** `dotnet run --project exercises/10-migration/10-04-lost-upload-response/src`
**Maps to:** special scenario **A (Ambiguous Upload Result)**

## Hints

1. "List every state the world could be in after that exception, not just the two obvious ones."
2. "The client cannot tell those states apart from where it is standing. Who can?"
3. "Ask the server what it has, and make the request idempotent so asking again - or sending again - is safe. Record the intent before acting."

Run the demo once before the interview so you know what it prints: the client gets an
`HttpRequestException`, the platform holds the file, and a second upload with the same
`Idempotency-Key` returns the **original** `fileId` without storing a duplicate.

## 1. Possible states of the world

A complete answer enumerates:

1. The request never arrived (DNS/TCP failure). Nothing happened.
2. It arrived, was rejected before processing (auth, validation). Nothing stored.
3. It arrived and was **partially** received; the platform discarded it. Nothing stored.
4. It arrived, was fully processed and stored, and the response was lost. **Stored.**
5. It arrived, was stored, and a post-processing step failed (virus scan, indexing). Stored in an unknown state.
6. It is **still being processed** right now (the client gave up first). Will be stored shortly.

Case 6 is the one people forget, and it is why "check immediately, then decide" can still be wrong.

## 2. What the SDK should do

- **Prefer prevention**: send an `Idempotency-Key` derived from stable identity + content (07-03), so the retry
  is safe no matter which state the world is in. The demo shows the platform returning the original result.
- **Otherwise reconcile**: `GET /migrations/{id}/files?name=…`, compare size and hash, and either accept it as done or upload again.
  Mind case 6: a short bounded wait (or a second check after a delay) before deciding.
- **Never**: silently mark it uploaded (10-03, incident 2) or fail the whole migration for one ambiguous file.
- The user should see "retrying", not an error; ambiguity is the SDK's problem, not theirs.

## 3. What to ask of the platform

Best to worst:

1. `Idempotency-Key` support with a documented retention window (the mock platform has it).
2. A way to query by client-supplied identity (`?name=`/`?clientFileId=`), so reconciliation is one cheap call.
3. Content-addressed storage (dedupe by hash server-side), which makes duplicates harmless.
4. Nothing: then dedupe locally after the fact and reconcile at the end of the migration, and be explicit in the
   product that duplicates are possible and how the customer resolves them.

"We can't change the API this quarter" is a realistic constraint: the answer should still be *safe* (never lose a file),
even if it costs duplicates. Duplicates are recoverable; missing files are not.

## 4. Scale changes the economics

| Size | What changes |
|---|---|
| 18 MB | Just re-upload. A reconciliation call costs more code than the bytes are worth (but still safer at 500 files) |
| 18 GB | Re-uploading is hours. Use resumable sessions (07-02): the session id makes the state queryable, and the ambiguity moves to the (cheap) `complete` call |
| 500 files | Reconcile **once** for the whole migration (list what the platform has) rather than once per file; batch the decision |

## 5. Exactly once?

No. Over an unreliable network you cannot have exactly-once *delivery*: the two-generals problem. What you can have is
**at-least-once delivery plus idempotent processing**, which gives an *effectively once* outcome. Say that out loud, and
then say what the customer is promised: "every file arrives, exactly one copy is kept, and we can prove it with hashes".

## 6. Testing it

- The mock platform's `ResponseLost` fault (this demo, and 07-03's tests).
- Crash injection at each step (10-03).
- A "chaos" run over the whole migration with random faults, asserting the invariant: *every source file appears exactly once on the platform*.
- Property-style: for every failure point in the sequence, the invariant holds.

## Follow-up questions

- "The hash on the platform differs from the local file. What now?" (The file changed, or corruption; re-upload as a new version; don't silently accept.)
- "Where does the idempotency key live so it survives a crash?" (Derived deterministically, so nowhere: that's the point. Or persisted with the checkpoint.)
- "How long must the platform remember keys?" (Longer than your longest retry/resume window; days.)
- "What if two clients migrate the same customer concurrently?" (Ownership/lease; see 16-03.)

## Scoring notes

| Level | Indicators |
|---|---|
| Weak | "Retry it" without seeing the duplicate risk, or "check if it exists" without seeing case 6 |
| Solid mid-level | Enumerates the states; proposes idempotency keys and/or reconciliation; prefers duplicates over data loss |
| Strong | Handles "still processing"; scales the strategy by file size and count; knows what to ask the platform for |
| Senior | Frames at-least-once + idempotency = effectively once; the two-generals limit; product-level promises; how to test the invariant systematically |
