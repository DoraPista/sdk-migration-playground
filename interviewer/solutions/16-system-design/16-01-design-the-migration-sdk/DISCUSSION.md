# 16-01 Design the Migration SDK – Discussion Notes

**Type:** system design · **Time:** 20 min · There is no correct architecture here. Use these as prompts.

## Hints

1. "Write the first ten lines of code an app author types against your SDK."
2. "The laptop reboots halfway through. Where is the state, and who wrote it?"
3. "Plan then run; a run object with an id so resume exists from day one; IProgress and CancellationToken throughout; a checkpoint the SDK owns; and no UI type anywhere in the library."

This is the exercise that most resembles the actual job. Everything the candidate proposes should be
pushed on once: *"what happens when that fails?"*

## A reasonable shape (one of several)

```csharp
// The app's first line:
var client = new MigrationClient(new MigrationClientOptions
{
    Endpoint = new Uri("https://platform.example/api"),
    Credential = new ClientSecretCredential(...),
    StateDirectory = "%LOCALAPPDATA%/Contoso/Migrations",
});

MigrationPlan plan = await client.PlanAsync(source, destination, ct);      // cheap, inspectable, serialisable
MigrationRun  run  = await client.StartAsync(plan, ct);                     // returns immediately
await foreach (var update in run.WatchAsync(ct)) { ... }                    // or IProgress<MigrationProgress>
MigrationResult result = await run.WaitAsync(ct);
```

Points worth having in some form:

- **Plan then run.** A plan the app can show, price and store is worth far more than one `MigrateAsync` call.
- **A run object with an id**, so `client.ResumeAsync(runId)` exists from day one.
- **`IProgress<T>` or `IAsyncEnumerable<T>`** for progress, never an event raised from a worker thread
  (events make every consumer solve the marshalling problem — see 11-02, 15-02).
- **`CancellationToken` on everything**, and cancellation that leaves the run resumable rather than ruined.
- **Options object, not twelve constructor parameters.**
- **`ILogger`/`ILoggerFactory` accepted, never created**; no `Console.WriteLine` in a library.
- A `TimeProvider` and an `HttpMessageHandler`/`IHttpClientFactory` seam so the thing is testable.

## The nine areas, and what to listen for

| Area | Good signals | Warning signs |
|---|---|---|
| Public API | Small surface; nouns the customer would recognise; async everywhere; no UI types | `static` entry points, `MigrateEverything(bool, bool, bool)`, returning `Task<string>` |
| Workflow | Discover → plan → authorise → transfer → verify → finalise, with a named state at each step | One method that does all of it |
| Auth | Token acquired and refreshed by the SDK, single-flight refresh, never logged, pluggable credential | Password string passed to every call; token in a static |
| Transfer | Chunked, resumable, streaming, hash per chunk and per file | `File.ReadAllBytes`, one PUT per file |
| Retries | Classified (retry 5xx/429/timeouts, never 400/401/403/404), jittered backoff, `Retry-After`, an overall budget | "I'd use Polly" with no policy behind it |
| Recovery | Checkpoint on disk, written atomically, reconciled against the server on resume | "We'd just start again"; trusting local state alone |
| Progress | Coalesced, monotonic, useful (bytes + files + current item), cheap to raise | Per-byte events; percentage only |
| Cancellation | Cooperative, leaves a resumable state, clean-up not governed by the cancelled token | `Thread.Abort`-thinking; abort with the cancelled token (13-02) |
| Persistence | SDK owns the checkpoint; app owns where it lives; documented format and version field | Nothing written until the end; state in memory only |

## The two-UI constraint

The strongest answers treat "one SDK, two apps" as the reason for the shape, not an afterthought:

- the SDK targets `netstandard2.0` (or `net8.0` + `netstandard2.0`) and references **no** UI assembly —
  checkable by a test (see 09-03),
- no `Dispatcher`, no `MainThread`, no `Application.Current`; the SDK hands out progress and the app marshals,
- no `async void`, no `.Result`, `ConfigureAwait(false)` throughout — otherwise it deadlocks in the WPF app (02-04),
- platform differences (file pickers, storage paths, connectivity) are the *app's* job, behind interfaces the
  SDK is given — this is the 12-04 lesson.

## Follow-ups

- "The consultant closes the lid for the night." → resumable by design; a run is a durable, named thing.
- "Where does the checkpoint live and what is in it?" (see 10-03 for the answer this repo uses)
- "How does the app show 'four hours remaining' honestly?" (bytes, not files; and be willing to say "unknown")
- "What is *not* in v1?" (Good: parallel destinations, delta migrations, a plug-in model. Bad: cancellation, resume.)
- "How do you test any of this?" (The whole of `shared/TestUtilities` is the answer this repo gives.)

## Level indicators

| Level | Indicators |
|---|---|
| Weak | One `Migrate(folder)` method; no state model; UI concerns inside the SDK; "we'd add retries later" |
| Solid mid-level | Plan/run split or equivalent; progress and cancellation designed in; retries classified; checkpoint on disk; keeps UI out |
| Strong | Justifies the API shape against both apps; single-flight auth; reconciliation on resume; names what is left out of v1 |
| Senior | Talks about the SDK as a product — versioning, diagnostics, what support sees, how it will be extended, and which decisions are hard to reverse |
