# 11-03 The App That Grows – Interviewer Notes

**Type:** WPF debugging (lifetime) · **Time:** 20 min · **Solution code:** `code/src/`

## What is wrong

Two roots keep every closed details view model alive:

1. **The application-lifetime monitor's event.** `monitor.ProgressChanged += OnProgressChanged` creates a strong
   reference from a long-lived object to a short-lived one. `Dispose()` only sets `_closed = true`, and nothing reads it.
   That is also why old views still react: they are still subscribed (report 2).
2. **The `DispatcherTimer`.** A running dispatcher timer is referenced by the dispatcher itself, and its `Tick`
   handler references the view model. `Stop()` is never called, so the timer keeps the view model alive *and* keeps firing.

And the shell never calls `Dispose()` at all, so even a correct `Dispose` would not have run.

## Hints

1. "After `CloseDetails()`, which objects still hold a reference to the details view model?"
2. "`Dispose` sets `_closed`. Who reads it?"
3. "Unsubscribe and stop the timer in `Dispose`, and make the shell actually dispose the view it drops."

## Intended solution

- `Dispose()` (idempotent): unsubscribe from the monitor, `Stop()` the timer and detach `Tick`.
- `ShellViewModel.Details` setter disposes the previous view model.
- Handlers guard against a late event arriving after disposal.

### Alternatives

- **Weak events**: `WeakEventManager<IMigrationMonitor, MigrationProgressEventArgs>.AddHandler(...)`, or implementing
  `IWeakEventListener`. Useful when the subscriber's lifetime is not controlled (e.g. a control that is never told it is gone).
  Discuss the cost: a leak becomes "it stops working" instead of "it grows", and the handler can still run after "closing" unless it is guarded.
- Push instead of pull: the shell subscribes once and forwards to the current details view model. Fewer subscriptions, clearer lifetime. A strong answer.
- `CompositeDisposable` / Rx subscriptions disposed together.
- For the timer: `CompositionTarget.Rendering` (same lifetime hazard) or a single app-wide timer that updates the current view.

## Common mistakes

- Unsubscribing but not stopping the timer (the GC test still fails, and the timer still fires).
- Stopping the timer but leaving the event (report 2 remains).
- Relying on the finalizer, or on `Details = null` alone.
- Making `MigrationMonitor` hold weak references *and* not fixing disposal, so "sometimes it works".
- Disposing in the *view*'s `Unloaded` event: `Unloaded` fires more than once in WPF (e.g. when a tab is switched), so it must be idempotent, and it may never fire at all in some container scenarios.

## How to find this in a customer's memory dump (follow-up)

Expect: capture with `dotnet-gcdump` / Task Manager "Create dump file" / `procdump`; open in Visual Studio's dump analyser or
`dotnet-dump analyze`; `dumpheap -stat -type MigrationDetailsViewModel` shows 300 instances; `gcroot <address>` shows
the path: `MigrationMonitor` → `ProgressChanged` delegate → target. A candidate who has done this once will recognise the shape
("long-lived object → event → dead view model") and say so before looking.

## Scoring notes

| Level | Indicators |
|---|---|
| Weak | Suggests `GC.Collect()`; blames WPF; only sets flags |
| Solid mid-level | Finds both roots; disposes from the shell; guards late events |
| Strong | Explains why the dispatcher keeps the timer alive; discusses weak events and their trade-offs; idempotent dispose |
| Senior | Proposes a design where the lifetime is obvious (shell owns one subscription), and explains dump-based diagnosis |
