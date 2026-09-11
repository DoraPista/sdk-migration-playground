# 02-06 The Server Rejects Every File – Interviewer Notes

**Type:** debugging (streams / HTTP) · **Time:** 20 min · **Solution code:** `code/src/ChecksumUploader.cs`

## What is wrong

1. **Consumed stream.** `ComputeSha256Async` reads the stream to the end. The same stream is then wrapped in
   `StreamContent`, which sends from the **current position**, so zero bytes are sent. The server hashes an empty body,
   which doesn't match the header, hence 422. **Empty files work** because the SHA-256 of zero bytes *is* the
   expected hash, which is the clue in the README.
2. **Reused request.** The retry loop resends the same `HttpRequestMessage`. `HttpClient` refuses:
   `InvalidOperationException: The request message was already sent. Cannot send the same request message multiple times.`
   Even if it didn't, the content stream would already be consumed.

## Hints

1. "Why would the server compute a different hash from yours? What exactly did it receive?"
2. "Where is the stream's `Position` after hashing? And what does the SHA-256 of an empty file look like?"
3. "Rewind or reopen the stream, and build a new request per attempt."

## Intended solution

Hash in a first pass, then create a fresh stream + `HttpRequestMessage` + `StreamContent` inside the retry loop.

### Alternatives

- `stream.Position = 0` before creating the content. It fixes defect 1 but not the retry, *unless* the
  request is also rebuilt per attempt (and each attempt rewinds).
- One `FileStream` for the whole method and `Seek(0)` before each attempt. Fine; mention that it only works for seekable sources (see 01-02).
- Hashing while uploading (a hashing stream) is **not** possible with this server contract, since the hash must be in a header. A
  strong candidate notes that a trailer or a separate "commit" call would enable single-pass uploads.

## Common mistakes

- Buffering the whole file into a `byte[]` "to make retries easy" (violates the constraint; see 07-01).
- Setting `Position = 0` once, before the loop, so attempt 2 still sends an empty body (after the request-reuse fix).
- Catching the `InvalidOperationException` and retrying.

## Follow-ups

- "How did this pass code review?" (No test against a real server contract; mocks that don't check the body.)
- "The file changes between the hash and the upload. What happens?" (422 from the server. Is that good behaviour? Yes: it fails loud.)
- "What does `StreamContent` do when you send it twice?" (It tries to rewind seekable streams; non-seekable ones throw.)

## Scoring notes

| Level | Indicators |
|---|---|
| Weak | Suspects the server or the hashing algorithm; tries hex casing changes |
| Solid mid-level | Finds the position bug and explains the empty-file clue; rebuilds the request per attempt |
| Strong | Explains *why* request messages can't be reused; keeps memory flat; notes seekability assumptions |
| Senior | Discusses single-pass designs and contract changes; testing against a contract-enforcing fake rather than permissive mocks |
