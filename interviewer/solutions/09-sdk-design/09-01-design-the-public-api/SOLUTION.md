# 09-01 Design the SDK's Public API – Interviewer Notes

**Type:** design (no single right answer) · **Time:** 30 min · **Reference design:** `reference/PublicApi.cs`

## Hints

1. "Start at the call site: write the ten lines a WPF developer types to run a migration."
2. "What does the caller need after starting - the id, progress, cancellation, the result? A bare Task gives them none of those."
3. "An options-constructed client; StartAsync returning a handle with Id, Progress, Completion and CancelAsync; immutable progress snapshots; a failed migration as a result, not an exception."

Do **not** give the candidate the reference API. Use it to compare against, and to ask "what about…?" questions.

## How to run this exercise

Give them 5 minutes to read `docs/requirements.md` and skim the internals, then design out loud, writing
signatures as they go. Interrupt with the questions from the README as they become relevant. A good session
ends with a page of public types and a usage snippet.

## What a good API does here

| Requirement | What to look for |
|---|---|
| Entry point | An **instance** type (`MigrationClient`) created from an options object, not a static/singleton. DI-friendly but not DI-requiring. Thread-safe |
| Start | `Task<MigrationHandle> StartAsync(request, ct)`: returns a *handle*, not just a Task, because the caller needs the id, progress and cancel |
| Progress | Immutable snapshot objects; documented thread ("raised on a background thread; UI hosts marshal"); **coalesced** (see 11-02 and 14-04). `IProgress<T>` parameter or an event: both fine, ask for the trade-off |
| Cancellation | `CancellationToken` on the call **and/or** `handle.CancelAsync()`. The handle matters: the UI's Cancel button is far from the code that started it |
| Completion | `handle.Completion` (`Task<MigrationResult>`), plus a terminal status in the result. "Failed" is a normal outcome, not an exception |
| Errors | Exceptions only for programming errors, auth and unrecoverable transport problems; per-file errors in the result with a **stable code** + user message |
| Several migrations | One client, many handles; a configured maximum; no ambient "current migration" |
| Logging | `ILoggerFactory` in the options; the SDK never writes files or configures sinks |
| Configuration | Options object with sensible defaults; credentials via an abstraction the host implements; `HttpMessageHandler` factory so hosts control proxies/certs |
| Resume | `GetResumableAsync()` + `ResumeAsync(migrationId)`; migration id is the durable handle |
| .NET Standard 2.0 | No `IAsyncEnumerable` without the package, no default interface methods, careful with records; `ValueTask` needs a package |
| Platform neutrality | No WPF/MAUI types anywhere; nothing that assumes a synchronization context |

## Questions to ask (and good answers)

- **"Which types are public?"** The entry point, the request/result/progress models, options, the credential abstraction, the exception hierarchy. Everything else internal (`InternalsVisibleTo` for tests). Fewer public types = fewer promises.
- **"Class or interface for the entry point?"** A sealed class is easier to evolve on .NET Standard 2.0 (no default interface methods). If hosts need to mock it, either ship an interface *you* own and document "do not implement", or design for a fake at a lower seam (an `HttpMessageHandler`). This trade-off is a strong signal.
- **"Progress on which thread?"** Anything but "the UI thread": the SDK doesn't know what a UI thread is. Document it, or let the host pass a `SynchronizationContext`/`TaskScheduler` if they want marshalling.
- **"What if a subscriber throws in a progress event?"** It shouldn't kill the migration.
- **"Why not expose `MigrationWorkflow` directly?"** Because then stages, checkpoints and HTTP become public contract.
- **"How does the WPF app show a per-file list?"** Per-file events or a snapshot list in progress. Watch out for candidates exposing `ObservableCollection` from the SDK (see 09-03).

## Common mistakes

- A static `MigrationManager.Instance` (see 09-02 for why).
- `event Action<string> StatusChanged` with human-readable strings as the only progress signal (untranslatable, unparseable).
- Exposing `Task RunAsync()` only, so cancel/progress need another object nobody has.
- Putting `CancellationTokenSource` in the public API instead of taking a token.
- Exposing internal enums (`Stage`) directly and then being unable to add a stage (see 09-05).
- Requiring DI (`IServiceCollection`) in a library that .NET Framework hosts use.
- Making everything `virtual`/`public` "for flexibility": every one is a promise.

## Scoring notes

| Level | Indicators |
|---|---|
| Weak | One `Start(folder)` method plus callbacks; no cancellation/result model; UI or platform assumptions |
| Solid mid-level | Instance client, async start, progress + cancellation + result model, options, no platform types, sensible public/internal split |
| Strong | Handle-based design, per-file error codes, documented threading, coalesced progress, credential abstraction, .NET Standard constraints respected |
| Senior | Talks about API evolution (what can be added later), mocking seams, telemetry/correlation, resumability as a first-class concept, and what is deliberately *not* in v1 |
