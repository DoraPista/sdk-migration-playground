# 09-04 The Customer's .NET Framework 4.8 Application – Interviewer Notes

**Type:** compatibility refactoring · **Time:** 25 min · **Solution code:** `code/`
**Maps to:** special scenario **I (.NET Standard compatibility)**

The work is mechanical; the value is in **knowing why each API is missing** and what the trade-offs are.

## What breaks when the target changes to `netstandard2.0`

| API in the code | Since | Replacement used in the solution |
|---|---|---|
| `File.ReadAllTextAsync` | .NET Standard 2.1 / Core 2.0 | `FileStream(useAsync: true)` + `JsonSerializer.DeserializeAsync` |
| `Path.GetRelativePath` | .NET Standard 2.1 / Core 2.0 | Small `PathCompat` helper (prefix + `Uri.MakeRelativeUri` fallback) |
| `SHA256.HashDataAsync` | .NET 5 | `IncrementalHash` + array-based `ReadAsync` |
| `Convert.ToHexStringLower` | .NET 9 (`ToHexString`: .NET 5) | `Hex.ToLower` with `b.ToString("x2")` |
| `Enumerable.Chunk` | .NET 6 | `EnumerableCompat.Chunk` |
| `ArgumentNullException.ThrowIfNull` | .NET 6 | `if (x is null) throw new ArgumentNullException(...)` |
| `await using` on `FileStream` | `IAsyncDisposable`: .NET Standard 2.1 | `using` (or add `Microsoft.Bcl.AsyncInterfaces` where it's really needed) |
| `Stream.ReadAsync(Memory<byte>)` / `WriteAsync(ReadOnlyMemory<byte>)` | .NET Standard 2.1 | Array overloads |
| `string.Contains(char)` | .NET Core 2.1 | `IndexOf(char) >= 0` |
| `record` / `init` | Needs `IsExternalInit` | One-line internal polyfill |
| `System.Text.Json`, `System.Net.Http.Json`, `TimeProvider` | Shared framework on .NET 10 | NuGet packages (`System.Text.Json`, `System.Net.Http.Json`, `Microsoft.Bcl.TimeProvider`) |

The tests are the safety net: the behaviour tests (relative paths, hashing, batching, JSON shape) must stay green,
which is exactly what stops the rewrite from quietly changing behaviour, for example in the hand-written `GetRelativePath`.

## Hints

1. "Change the target framework and read the first ten errors. What do they have in common?"
2. "For each one: was it added after .NET Standard 2.0, or is it in a package?"
3. "Keep the polyfills internal and small, and lean on the tests to prove the behaviour didn't move."

## Discussion: what does .NET Standard 2.0 cost us?

- **No `Span`/`Memory`-based I/O overloads** (without `System.Memory`), so extra copies in hot paths.
- **No `IAsyncEnumerable`** without `Microsoft.Bcl.AsyncInterfaces`, no `await using` on framework types.
- **No default interface methods**, which limits how interfaces can evolve (see 09-05).
- **No `HttpClient` niceties** (`HttpClient.Timeout` behaviour differs; no `SocketsHttpHandler`, no `PooledConnectionLifetime`).
- Nullable reference types work, but `[NotNullWhen]`-style attributes are missing (can be polyfilled).
- Trimming/AOT metadata, `System.Text.Json` source generators: limited.
- Every polyfill is code you own and test.

**When would you not accept the cost?** When no .NET Framework consumer exists; when the library is performance-critical
(Span-based APIs matter); or when you can **multi-target** instead.

### Multi-targeting: the alternative worth raising

```xml
<TargetFrameworks>netstandard2.0;net8.0</TargetFrameworks>
```
with `#if NET8_0_OR_GREATER` around the fast paths. Best of both, at the cost of a build matrix, conditional code and
double the testing. A strong candidate proposes it and then says why a single target is fine *for this library right now*
(the team already decided, and the code is not hot-path critical).

## Common mistakes

- Multi-targeting when the exercise says single target (not wrong, but read the constraints; ask).
- Re-implementing `GetRelativePath` with `Substring` only, which breaks for `..` cases or trailing separators (a behaviour test covers the trailing separator).
- `.Result`/`.GetAwaiter().GetResult()` "because the async overload is missing".
- Replacing `IncrementalHash` with `sha.ComputeHash(stream)` (blocking, and reads the whole file synchronously) without noticing it's synchronous.
- Forgetting that the **package** versions must support .NET Standard 2.0 (`System.Text.Json` does; some newer ones don't).
- Leaving polyfills `public`, which puts them in the SDK's API surface forever.

## Follow-ups

- "The customer moves to .NET 8 next year. What do you do then?" (Add a `net8.0` target, or drop `netstandard2.0` in a major version.)
- "How would you verify the DLL really loads in a .NET Framework app?" (The `LegacyHost` sample; and a smoke test on a Framework runner.)
- "What about `netstandard2.1`?" (Nothing supports it except .NET Core 3.x/Xamarin: for a Framework host it's useless.)

## Scoring notes

| Level | Indicators |
|---|---|
| Weak | Adds `#if` everywhere, or copies whole BCL implementations; breaks behaviour tests; doesn't know why APIs are missing |
| Solid mid-level | Retargets, fixes every error with small internal helpers and the right packages, tests stay green |
| Strong | Explains each missing API's origin, keeps polyfills internal, spots the hidden behaviour risk in the relative-path helper |
| Senior | Frames the multi-targeting trade-off, the long-term cost of .NET Standard, and a plan for when the customer upgrades |
