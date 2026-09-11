# 09-02 Review: MigrationManager – Interviewer Notes

**Type:** code review / API design · **Time:** 25 min · No code to write

## Hints

1. "Read it once for what would hurt a customer, not for style. What is the single worst thing in this file?"
2. "Look at what is static, what gets written to the log, and what happens when a second migration starts in the same process."
3. "Secrets in public statics and in the log file, a TLS validation switch, shared mutable statics, blocking .Result, swallowed exceptions. Rank them and say which one blocks the release."

This exercise measures **reading, prioritising and communicating**. There are far more defects than anyone can list
in 25 minutes, so what matters is *which* ones the candidate raises first and how they justify them.

## Defects, grouped

### A. Security (blocking)

| # | Defect | Consequence |
|---|---|---|
| A1 | `MigrationConfig.ClientSecret` is a public static mutable field; a constructor also takes `password` / `clientSecret` and stores them in public fields (`UserName`, `Password`) | Secrets in memory, in dumps, and readable/writable by any code in the process (including partner plug-ins) |
| A2 | `Log("Token response: " + json)` writes the **access token** to a log file in `C:\Temp` | See 06-02. Plus `C:\Temp` is world-writable: any user on the machine can read it |
| A3 | `SkipCertificateValidation` flag in a shipped SDK | One support call away from disabling TLS validation in production |
| A4 | Token built by string splitting; form body built by string concatenation without URL encoding | Breaks (or worse) for secrets containing `&`, `+`, `=` |

### B. Correctness and threading (blocking)

| # | Defect | Consequence |
|---|---|---|
| B1 | `Start()` is fire-and-forget `Task.Run`: no way to await, no error, no completion, exceptions only reach a callback | The caller can't know when it finished or if it failed (see 02-01) |
| B2 | `async void Cancel()` | Exceptions crash the process; cancellation isn't awaited; it doesn't actually stop the loop promptly (`_cancelled` is only checked between files, and never reaches HTTP calls: see 03-02) |
| B3 | `StartSync` calls `.Wait()` → deadlock when called from a UI thread (see 02-04) |
| B4 | `UploadFile` uses `.Result` internally | Same, plus thread-pool starvation with 16 "upload threads" |
| B5 | Mutable shared statics (`Instance`, `Http`, `MigrationsRun`, `MigrationConfig.*`) | Two migrations at once corrupt each other's state; no way to serve two customers (see 02-05) |
| B6 | `Http.DefaultRequestHeaders` mutated per migration | Cross-talk between concurrent migrations; not thread-safe |
| B7 | Retry loop: `break` only on success, otherwise silently continues and the file is **marked "Uploaded" anyway** | Silent data loss: the headline defect |
| B8 | `Thread.Sleep(1000)` inside the retry, on a pool thread | Blocking; no backoff/jitter; retries everything including 4xx |
| B9 | `percent = i * 100 / files.Count` | Never reaches 100%; `DivideByZeroException` for an empty folder |
| B10 | `Files` (`DataTable`) mutated from a background thread while the UI binds to it | `InvalidOperationException` / corrupted UI (see 11-02) |
| B11 | Finalizer disposing a **static** `HttpClient` | Disposes a shared client at an arbitrary time; finalizers should never touch other objects |
| B12 | `Clone()` = `MemberwiseClone` | Shares the `DataTable` and the pending list between "copies" |

### C. Platform coupling (blocking for MAUI and partners)

| # | Defect |
|---|---|
| C1 | `MigrationConfig.UiDispatcher` (WPF `Dispatcher`) in the SDK's configuration |
| C2 | `OnThumbnail(BitmapImage)`: a WPF type in a callback, created on a background thread (and `BitmapImage` isn't frozen, so the UI can't use it) |
| C3 | `ShowSettingsDialog(Window owner)` and `MessageBox.Show` inside the engine: an SDK must never own UI |
| C4 | `OpenCheckpointDatabase()` returns `IDbConnection` (and `null`): leaks a storage implementation detail into the public API |
| C5 | `DataTable` as the progress model: heavy, WinForms-era, awkward outside .NET Framework |
| C6 | Hard-coded Windows paths (`C:\Temp`) |

### D. API design and maintainability

- `Instance` **and** public constructors: which is it?
- Public mutable fields everywhere (can't validate, can't evolve, not binding-friendly).
- `bool Validate(string, out string errors)` with a newline-joined string instead of structured results (see 01-03).
- `object? Tag`.
- No cancellation token, no `IProgress<T>`, no async, no logging abstraction (writes its own file).
- Everything is public: nothing can change without breaking partners (see 09-05).
- No way to migrate a list of files, only a folder; no way to resume.

## What "the worst three" should sound like

Good answers pick defects by **impact**, not by how easy they are to spot. Typically:

1. **B7 – silent data loss.** Files are recorded as uploaded after all retries failed. The customer decommissions their file server. This is the one that ends careers.
2. **A1/A2 – secrets.** A live token in a world-readable log, secrets in mutable statics.
3. **B5/C1–C3 – the API cannot serve MAUI or partners at all**, so adopting it doesn't meet the business goal. (Or B3's deadlock, which is what their own support tickets are full of.)

## The redesign sketch (what to look for)

- An instance-based, injectable entry point (`MigrationClient` / `IMigrationClient`) created from an options object; no statics.
- `Task<MigrationResult> RunAsync(MigrationRequest, IProgress<MigrationProgress>?, CancellationToken)`.
- Platform-neutral progress and results (no WPF types, no `DataTable`); thumbnails as bytes or a stream, or not in the SDK at all.
- Errors: typed exceptions or a result model; per-file outcomes.
- `ILogger` from the host, not a log file.
- Credentials supplied through an abstraction (`ITokenCredential`) so the host decides where secrets live.
- Only a small surface public; everything else `internal` (+ `InternalsVisibleTo` for tests).

## Follow-up questions

- "The partner integrations call `StartSync`. How would you migrate them off it?" (Keep a shim, `[Obsolete]`, a major version, see 09-05.)
- "Which of these would your analyzers or code review have caught automatically?" (`async void`, `.Result`, CA2007, secrets scanning, nullable.)
- "How would you test this code as it stands?" (You can't, really: statics and no seams. That itself is a blocking finding.)
- "Management asks: can we ship it as-is to buy time?" (A judgment question. Watch for: silent data loss and the TLS flag are non-negotiable; the rest can be staged.)

## Scoring notes

| Level | Indicators |
|---|---|
| Weak | Lists style issues (naming, comments); misses the silent data loss; "it works, so ship it" |
| Solid mid-level | Finds the threading, statics, secret logging and platform coupling; explains consequences; suggests an instance-based async API |
| Strong | Prioritises by production impact; spots B7 and B9; knows why `async void`/`.Result` are dangerous; proposes a concrete, minimal public surface |
| Senior | Frames adoption as a decision (cost of rewrite vs wrap vs adopt), migration path for partners, API-evolution constraints, and what to enforce in CI so it doesn't come back |
