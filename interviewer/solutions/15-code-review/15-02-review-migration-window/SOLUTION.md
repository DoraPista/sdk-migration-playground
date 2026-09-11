# 15-02 Review This PR: The Migration Window – Interviewer Notes

**Type:** code review (WPF) · **Time:** 25 min · **No code to fix.**

## Hints

1. "Walk me through the window for a folder with one 5 MB file. Then for a folder that does not exist."
2. "Which thread runs all of this? What is started and never stopped? Which exit paths leave the button disabled?"
3. Name one: the progress counts chunks while the arithmetic assumes files. Then ask what else QA would not have noticed.

This PR is the counterpart to 15-01: the problems are about **threading, lifetime and where code lives**
rather than HTTP. "QA signed it off" is deliberate — several of the worst findings are invisible in a demo.

## Blocking findings

| # | Where | Finding | What the user sees |
|---|---|---|---|
| 1 | `MigrationWindow.xaml.cs`, `OnFileProgress` | **`FileProgress` fires per 64 KB chunk, but `_done` is treated as a file count**: `_done * 100 / e.Total` | On the first 1 MB file the bar jumps past 100 % and stays there. QA's 500-file project had small files, so it looked plausible |
| 2 | `RunMigration`, the `Directory.Exists` check | Early `return` after `MessageBox.Show("Folder not found")` — the button was already disabled and the timer already started | One typo in the folder and the window is dead: Start greyed out for ever, the elapsed counter still running. Only closing the window recovers |
| 3 | `MigrationRunner.Run` | The whole migration runs **on the UI thread**. There is no `ConfigureAwait(false)` in the library, so every `await` resumes on the captured context | The window is unresponsive for the entire migration. It "works" only because nothing ever leaves the UI thread — which also means the progress updates work *by accident* |
| 4 | `GetFolderSize` (and `Directory.GetFiles`) | Walks the whole tree synchronously on the UI thread before starting | For a real export the window is frozen and white before anything happens |
| 5 | `MigrationRunner.Run` | `if (sent != total) { }` — an empty block with a comment claiming the next run will pick it up. Nothing implements that | **Silent data loss.** The migration reports success having uploaded part of a file |
| 6 | `MigrationWindow.xaml` | The API key is a hard-coded default in a plain `TextBox` — committed to source control, visible on screen, shoulder-surfable | A key in the repository is a key that has leaked. Also `ServerUrl` is a `const` in the window |
| 7 | Everywhere | **No cancellation.** Closing the window does not stop the migration; there is no Cancel button | The user cannot stop a nine-hour migration started by mistake; closing the window leaves it running until the process exits |
| 8 | `_done` | Never reset between runs | A second migration in the same window starts at whatever the first one left |

## Worth raising, not necessarily blocking

| # | Finding | Note |
|---|---|---|
| 9 | `Directory.GetFiles(folder)` in `Run` vs `SearchOption.AllDirectories` in `GetFolderSize` | The size shown and the files sent disagree — subdirectories are counted but never uploaded |
| 10 | Progress event per 64 KB | 8,000 events for a 500 MB project, each doing string concatenation and a layout pass. Coalesce (see 11-02), or report a percentage with a minimum interval |
| 11 | Events instead of `IProgress<T>` | Events put the thread-marshalling burden on every subscriber and are easy to leak. `IProgress<T>` captures the context at construction |
| 12 | `_timer` is never stopped; `_runner` events are never unsubscribed | The `DispatcherTimer` keeps the window alive after it is closed (see 11-03) |
| 13 | `MessageBox.Show` in the completion path | Untestable, blocks the dispatcher, and fires even if the window is closing |
| 14 | `_done * 100 / e.Total` | Integer division, and `DivideByZeroException` for an empty folder |
| 15 | `DateTime.Now` for elapsed time | Use `Stopwatch`; a DST change or a clock sync makes the counter jump or go negative |
| 16 | The shared 64 KB `buffer` passed to an async `Send` | Fine today because `Send` copies nothing, but it is a trap: any caller that keeps the buffer sees it overwritten |
| 17 | `FileProgressEventArgs` public mutable fields, `FileName` as a full path | The view does `Path.GetFileName` — the event should carry what the UI needs |
| 18 | No MVVM, although the rest of the product uses it | Not dogma: the point is that **none of this can be tested** without a window. That is the reason to move it, and it is worth doing now because items 1, 2, 5 and 8 are exactly what a view-model test would have caught |
| 19 | No tests, and the button is re-enabled in only one of three exit paths | |

## "Async void is allowed for event handlers"

The author is right about the rule and wrong about the consequence. `async void` is fine *for the handler*,
but only if it handles its own errors on every path — this one has an early `return` and leaves the UI
in a broken state, and the `catch` does not re-enable the button or stop the timer. The interesting answer is
"yes, and that is why the handler should do nothing but call a command on a view model that you can test".

## What distinguishes the levels

**Junior** — "it should be MVVM", `MessageBox` is ugly, the API key is hard-coded. Rule-based, not effect-based.

**Mid-level** — finds the progress arithmetic, the early return leaving the UI stuck, the missing cancellation,
the timer that is never stopped, and the hard-coded key. Explains what the user experiences. Asks whether the
progress event fires per file or per chunk instead of assuming.

**Senior** — also:
- spots that the migration runs on the UI thread and that the progress updates work *by accident* — and that
  "fixing" the runner with `ConfigureAwait(false)` would immediately produce cross-thread exceptions in the view,
- identifies the silent partial-upload as the most serious finding, because it is invisible and loses customer data,
- frames the MVVM change as testability rather than style, and names the two or three tests they would write,
- asks operational questions: what does the user do after a failure, is the migration resumable, what does
  support see, and what happens if the machine sleeps mid-migration.

## Facilitating

- If they miss the chunk/file mix-up: *"Walk me through the bar for a folder with one 5 MB file."*
- If they say "move it to MVVM" and stop: *"Which bug does that prevent? Which does it not?"*
- If they miss the threading point: *"Which thread runs `OnFileProgress`? What would change if the runner used `ConfigureAwait(false)`?"*
- Ask the closing question: *"QA approved this. What would you change about how it was tested?"*

## Scoring notes

| Level | Indicators |
|---|---|
| Weak | Only style and MVVM; misses the broken progress and the stuck UI |
| Solid mid-level | 6–10 findings including the progress arithmetic, the early return, cancellation and the key; prioritises |
| Strong | Also the UI-thread issue, the partial-upload data loss, event flooding, and the leak |
| Senior | Explains the accidental correctness of the threading, names the tests, and weighs "fix now vs restructure" honestly |
