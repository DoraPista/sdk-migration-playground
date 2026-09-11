# 12-03 Killed in the Background – Interviewer Notes

**Type:** MAUI lifecycle discussion + small fix · **Time:** 20 min · **Solution code:** `code/` (overlays `MauiGym`)

## Hints

1. "Write down what this code assumes it will be told before the app goes away."
2. "Which of those callbacks is actually guaranteed when Android or iOS kills a backgrounded process?"
3. "Save state on every meaningful change and when backgrounding, write it atomically, and reconcile with the platform on restart rather than trusting in-memory progress."

## The assumptions that don't hold

| Assumption in the code | Reality on a phone/tablet |
|---|---|
| "We are told before the app goes away" (`window.Destroying` writes the state) | Android and iOS can kill a backgrounded process **with no callback at all**. `Destroying` is reliable on Windows, not elsewhere. `Stopped`/`OnSleep` fire when backgrounding, but not when the OS kills you later |
| "`Task.Run` keeps running while the user is in another app" | The process is suspended. On iOS you get a few seconds after backgrounding; on Android the work is throttled and the process may be killed. The loop simply stops |
| "Writing the file is atomic" | A kill during `File.WriteAllText` leaves a truncated file, and the next start can't read it (see 10-03) |
| "In-memory progress is the truth" | Symptom 2 (40 in the app, 12 in the portal) is exactly this: the app counted files it *started*, and the platform is the only place that knows what arrived |

## The fix in the code

- Write the checkpoint **after every file**, atomically (temp + `File.Move(..., overwrite: true)`).
- Restore on start and on `Resumed`; tolerate an unreadable checkpoint.
- Keep the lifecycle hooks as an optimisation, not as the mechanism.
- (From 10-03: reconcile with the platform on resume, because the local count can be optimistic.)

## What real background execution costs (the discussion)

| Platform | What is actually available |
|---|---|
| **Android** | A **foreground service** with a persistent notification (`dataSync` type since Android 14, with new limits and a 6-hour/day budget), or `WorkManager` for deferrable work. Both need platform code and user-visible notifications |
| **iOS** | Background *transfers* via `NSURLSession` background sessions (the system uploads, your app may be terminated), `BGProcessingTask` for deferrable work. Long arbitrary background execution is not available |
| **Windows** | The process keeps running while the window exists; closing it ends the migration |

The product recommendation a senior candidate reaches: **resumable, idempotent uploads plus a visible "keep the app open"
UX**, and platform-specific background transfer only where it earns its cost. This is also why the SDK must never
assume it owns the process lifetime (see 09-01 and 10-03).

## Follow-up questions

- "Which MAUI lifecycle events would you subscribe to, and what would you do in each?" (`Window.Created/Activated/Stopped/Destroyed/Resumed`; `App.OnSleep`/`OnResume` are the older Xamarin-era hooks. Save on `Stopped`, restore on `Resumed`, never rely on `Destroying`.)
- "Where should the checkpoint live?" (`FileSystem.AppDataDirectory`; not `Preferences`, which is for small settings, not progress records.)
- "The user force-quits during an upload. What does the next start show?" (Resumed progress, reconciled with the platform; nothing lost, at worst one file repeated.)
- "How would you test this?" (Kill the app from the OS; on Android, `adb shell am kill`, or *Don't keep activities* in developer options.)

## Scoring notes

| Level | Indicators |
|---|---|
| Weak | Adds more lifecycle handlers; assumes `Destroying` is enough; keeps progress in memory |
| Solid mid-level | Checkpoints per file, atomic write, restore on resume; names the killed-process reality |
| Strong | Distinguishes suspension from termination; knows `Preferences` is the wrong store; mentions reconciliation with the platform |
| Senior | Gives a platform-by-platform background story with costs, and a product recommendation the team could act on |
