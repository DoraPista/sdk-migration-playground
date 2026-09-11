# 07-01 The 100 GB File – Interviewer Notes

**Type:** debugging / performance (memory) · **Time:** 25 min · **Solution code:** `code/src/LargeFileUploader.cs`
**Maps to:** special scenario **E (100 GB File)**

## What is wrong

- `File.ReadAllBytesAsync` loads the **whole file** into a single `byte[]`:
  - > 2 GB: impossible (array/`ReadAllBytes` limit), hence the `IOException` for the 100 GB scan.
  - 1.8 GB: one Large Object Heap allocation of 1.8 GB, plus whatever the OS page cache does. The working set explodes on 8 GB laptops, then paging and OOM.
- `SHA256.HashData(bytes)` and `ByteArrayContent` make it worse only a little (no extra copy), but the damage is done.
- Progress is reported once, **after** the response, so the bar sits at 0% then 100%.

## The four questions (the spec's performance format)

1. **Likely bottleneck?** Memory, and then paging. CPU and network are fine.
2. **How to measure?** `dotnet-counters monitor -n <app> System.Runtime` (GC heap size, LOH size, working set); Visual Studio Diagnostic Tools; `GC.GetTotalAllocatedBytes` in a test like the one here. *Measure before and after.*
3. **Change?** Stream: hash pass over a `FileStream`, then a streaming `HttpContent` with declared `Content-Length` and progress.
4. **Trade-off?** The file is read **twice** (2× disk I/O, and a torn read if the file changes between passes). Alternatives below avoid the double read but need contract changes.

## Hints

1. "How much memory does line 1 of `UploadAsync` need for a 100 GB file?"
2. "The server needs the hash *before* the body. What does that force you to do if you can't hold the file in memory?"
3. "Hash in one streaming pass, then upload with a streaming `HttpContent` that reports progress and declares its length."

## Intended solution

Two streaming passes; custom `ProgressStreamContent` (or `StreamContent` over a progress-reporting stream wrapper that
still exposes `Length`/`CanSeek` so `Content-Length` can be computed); pooled buffers; `SequentialScan`.

### Alternatives worth discussing

| Option | Trade-off |
|---|---|
| Hash while uploading + send the hash **afterwards** (trailer or a separate "commit" call with the hash) | Single read; needs a contract change (the resumable API in 07-02 does this with `complete`) |
| Chunked/resumable upload with per-chunk hashes | Best for 100 GB over bad networks (see 07-02); more requests |
| `StreamContent` over a wrapper stream | Fine if the wrapper exposes `Length` (else chunked encoding: fails the Content-Length test) |
| Memory-mapped file | Doesn't really help HTTP streaming; adds complexity |
| Reading once into a temp buffer file | Pointless for local files; valid for non-seekable sources (01-02) |

## Common mistakes

- Replacing `ReadAllBytes` with `new MemoryStream()` + `CopyTo`: same memory.
- `StreamContent` wrapped in a stream without `Length`, so `Transfer-Encoding: chunked` and the gateway rejects it (the `Content_length_is_declared` test).
- Reporting progress on **read** rather than on **write** (close enough; discuss what "uploaded" means with buffering in the network stack).
- Reporting progress on every 4 KB with a UI subscriber that marshals synchronously, which floods the UI (see 11-02).
- Forgetting `FileShare.Read` (fails when the CAD app has the file open read-only).

## Follow-ups

- "The file changes while we upload it (the scanner is still writing). What happens?" (422 checksum mismatch. Is that OK? Detect with size/timestamp before and after; retry later.)
- "What's the throughput bottleneck now?" (Probably SHA-256 on older CPUs at 500 MB/s–1 GB/s, or disk; parallel hashing of the next file while uploading the current one.)
- "Why `HttpCompletionOption.ResponseHeadersRead`?" (Not important for uploads; important for downloads.)
- ".NET Framework host?" (No `SHA256.HashDataAsync`, no `ArrayPool` without System.Buffers. See 09-04.)

## Scoring notes

| Level | Indicators |
|---|---|
| Weak | "Increase memory"/"use 64-bit"; buffers into MemoryStream; can't explain the 2 GB limit |
| Solid mid-level | Two-pass streaming, correct headers, progress during upload, Content-Length declared |
| Strong | Explains LOH/working set, how to measure, the double-read trade-off and torn reads; pooled buffers |
| Senior | Proposes contract changes (trailing hash / chunked resumable), end-to-end integrity, parallelising hashing and I/O |
