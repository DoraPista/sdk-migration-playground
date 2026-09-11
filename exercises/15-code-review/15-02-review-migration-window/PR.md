# PR #491 – Migration window with live progress

**Author:** @sam · **Branch:** `feature/migration-window` → `main` · **Files changed:** 3 · **+171 −0**

## What this does

Adds the window the user sees while a migration runs: a progress bar, the current file, and a Start button.
`MigrationRunner` does the work and raises an event per file; the window subscribes and updates the bar.

## Notes for the reviewer

- I put the code in the code-behind because there is no real logic here, just UI.
- The progress event fires per file so the bar moves smoothly. For the big project it fires a lot, but WPF
  seems to keep up.
- `RunMigration` is `async void` because it is an event handler, which I understand is the one place that is allowed.
- I show a `MessageBox` on failure so the user knows something went wrong.
- The server URL and the API key are in the window for now; we can move them to config later.
- QA ran it against the mock server with the 500-file project and it worked.
