# 01-02 Where Do Files Come From? – Interviewer Notes

**Type:** API design + implementation · **Time:** 25 min · **Solution code:** `code/`

This is a design exercise wearing an implementation costume. The code is short. What you are
evaluating is the reasoning behind the abstraction.

## The key insights

1. **"A stream" is the wrong abstraction.** The SDK reads content at least twice: once to hash and once to
   upload, plus again on every retry. Accepting a `Stream` means the SDK either buffers the whole thing
   (bad for 30 GB) or can't retry. The abstraction must be **re-openable**: "something that gives me a new stream when asked."
2. **Ownership must be explicit.** Whoever calls `OpenReadAsync` owns the returned stream and disposes it.
   Nothing is opened until needed, so a queue of 10,000 sources holds no handles.
   *(The current `MigrationManifestBuilder` does not actually hold handles. It uses `FileInfo`. A strong
   candidate checks instead of assuming; the risk is in a naive stream-based redesign.)*
3. **Length can be unknown.** Picker streams on Android and zip entries on some platforms. Progress, manifests and
   `Content-Length` must cope, for example by measuring while hashing.
4. **No platform types in the SDK surface.** No `FileResult` (MAUI), no `StorageFile` (WinRT), no `ZipArchiveEntry`.
   Adapters live in the host app or in thin platform packages.

## Hints

1. "How many times does the SDK need to read the content of a file?"
2. "What happens on retry if the only thing you have is a `Stream` you've already read?"
3. "What if the abstraction handed out a *new* stream every time it was asked, and the SDK disposed each one?"

## Reference design (see `code/src/SourceFile.cs`)

```csharp
public abstract class SourceFile
{
    public string Name { get; }
    public long? Length { get; }
    public DateTimeOffset? LastModified { get; }
    public string? ContentType { get; }
    public abstract Task<Stream> OpenReadAsync(CancellationToken ct = default);

    public static SourceFile FromPath(string path);
    public static SourceFile FromBytes(string name, ReadOnlyMemory<byte> content, string? contentType = null);
    public static SourceFile FromStreamFactory(string name, Func<CancellationToken, Task<Stream>> openRead, ...);
}
```

`UploadService` hashes on the first open (which also measures the real length) and streams on the second.
The string-path overload is kept for source compatibility.

### Alternative valid designs

| Design | Trade-off |
|---|---|
| `interface ISourceFile` instead of an abstract class | More mockable and familiar. But adding a member later is a breaking change on .NET Standard 2.0, where there are no default interface methods. A strong answer to the versioning question |
| `Func<Stream>` parameter | Minimal, but loses name/length/metadata and can't be extended |
| Accept `Stream` + `bool canReopen` | Honest, but pushes buffering decisions to every caller |
| Buffer non-reopenable sources to a temp file inside the SDK | Valid fallback for one-shot streams; must be explicit about disk usage and cleanup |
| `IAsyncEnumerable<ReadOnlyMemory<byte>>` content | Elegant for streaming, awkward for `HttpContent` and hashing |

## Common mistakes

- `UploadAsync(Stream stream)`, then trying `stream.Position = 0` for the second pass (non-seekable streams throw).
- Opening every file when building the manifest "to get the length".
- Putting `FileResult` (MAUI) or WPF `Uri` types into the SDK.
- Returning the same `Stream` instance from every `OpenReadAsync` call.
- Forgetting `Content-Length` when the source length was unknown (the request falls back to chunked encoding, which some proxies reject).
- Ignoring cancellation during hashing.

## Clarifying questions worth rewarding

- Can a file change between hashing and uploading? (It can. Compare the length and hash after the upload, or hash while uploading and verify on the server.)
- Is a one-shot stream source a real requirement, or can every source be reopened?
- Is `LastModified` needed by the server, or only for the local manifest?
- What's the largest file? (It decides whether buffering is ever acceptable.)

## Follow-ups

- "The MAUI team says the picker's stream can only be read once. What now?" (Copy it to app cache storage with a length check, or mark it non-retryable and say so in the API.)
- "How would you add a `Checksum` the source already knows (e.g. from a DMS) to avoid the first pass?" (An optional `KnownSha256` property; verify on the server.)
- "How would this look in a .NET Standard 2.0 library?" (No `IAsyncDisposable` without a polyfill, and `ValueTask` needs a package.)

## Scoring notes

| Level | Indicators |
|---|---|
| Weak | Replaces `string path` with `Stream`; doesn't think about the second read or retries; leaks platform types |
| Solid mid-level | Re-openable abstraction; clear disposal ownership; two working source types; keeps the path overload |
| Strong | Handles unknown length; discusses interface vs abstract class for versioning; checks whether handles are really leaked instead of assuming |
| Senior | Raises content changing between passes; one-shot stream policy; where adapters live (host vs platform packages); how this shapes the public SDK surface |
