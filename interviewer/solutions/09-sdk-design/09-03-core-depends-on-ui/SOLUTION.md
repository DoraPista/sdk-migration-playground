# 09-03 The Core That Needs a Window – Interviewer Notes

**Type:** refactoring / architecture · **Time:** 30 min · **Solution code:** `code/`
**Maps to:** special scenario **H (MAUI/WPF separation)**

## What is wrong

| Symptom | Cause |
|---|---|
| MAUI can't reference the core | The project targets `net10.0-windows` with `UseWPF`. A MAUI Android build can't reference a Windows-only assembly |
| Console service crashes with `NullReferenceException` | `Application.Current` is `null` outside a WPF app, and the coordinator calls `Application.Current.Dispatcher` for every file |
| A dialog blocked an unattended machine | `MessageBox.Show` inside the core on a failure. A library must never decide to show UI |
| (also) | `ThumbnailService` uses `BitmapImage` (WPF imaging) although `IThumbnailRenderer` exists and is **ignored** by the constructor |
| (also) | `ObservableCollection<FileItem> Files` is UI state owned by the library; mutated from a background thread it throws in WPF bindings anyway (see 11-02) |

## Hints

1. "Which of these lines could not run in a console application?"
2. "The constructor takes an `IThumbnailRenderer`. Where is it used?"
3. "Move everything the core can't know about (dispatcher, dialogs, imaging, list state) into the host, behind interfaces or events."

## Intended solution

- Core → `net10.0` (platform-neutral; `netstandard2.0` if .NET Framework hosts matter, see 09-04). No `UseWPF`.
- Progress and per-file status as **events with plain data**; no dispatcher. Document that events come from a background thread and that hosts marshal.
- No dialogs: failures are in the result and in a `FileStatusChanged` event.
- Thumbnails via the injected `IThumbnailRenderer`; the WPF implementation moves to the demo app.
- The UI list belongs to the UI: the window keeps its own `ObservableCollection`.
- Guard host callbacks so a throwing subscriber can't fail the migration.

### Alternatives

- Keep a `SynchronizationContext`/`TaskScheduler` option so hosts *can* ask for marshalled callbacks. Reasonable; the default must still be "no context needed".
- `IProgress<T>` instead of events: `Progress<T>` captures the creating context, which does the marshalling for the host automatically. Nice, and worth discussing (it also reorders/floods; see 14-04).
- Multi-targeting (`net10.0;net10.0-windows`) with WPF helpers in the Windows target. Possible, rarely worth it; the host is the better place.

## Enforcing it

The two architecture tests are the point of this exercise: they turn "please keep the core clean" into a build failure.
Ask what else could enforce it: a separate solution folder without UI references, NetArchTest/ArchUnitNET, `dotnet-apicompat`,
or simply the fact that the MAUI project references the core (a build break if someone adds WPF back).

## Common mistakes

- Replacing `Application.Current.Dispatcher` with `Dispatcher.CurrentDispatcher` (still WPF, and it creates a dispatcher for the calling thread that nobody pumps: the callbacks never run).
- Keeping `MessageBox` behind `if (Application.Current != null)`.
- Moving the WPF types into an `#if WINDOWS` block in the core.
- Exposing `event Action<BitmapImage>` after the refactor (same problem, new place).
- Passing the renderer but rendering **before** checking the file is an image, or letting a renderer exception fail the file.

## Follow-ups

- "Where should the core get a thumbnail for the console service?" (Nowhere: `null` renderer, no thumbnails. The nicety is optional by design.)
- "The MAUI app wants progress on the main thread. Whose job is that?" (The host: `MainThread.BeginInvokeOnMainThread`; see 12-02.)
- "What about `ObservableCollection` in the SDK: is it platform-specific?" (No, it's in `System.ObjectModel`, but it's a *UI-shaped* API: the SDK shouldn't own view state.)

## Scoring notes

| Level | Indicators |
|---|---|
| Weak | Adds null checks around `Application.Current`; keeps WPF in the core; doesn't notice the unused renderer |
| Solid mid-level | Platform-neutral core, events with plain data, renderer used, dialogs gone, WPF demo still works |
| Strong | Explains threading contract for events; moves list state to the host; guards subscriber exceptions; keeps the architecture tests meaningful |
| Senior | Talks about enforcing boundaries in CI, multi-targeting trade-offs, `IProgress<T>` vs events, and what the core must promise MAUI hosts |
