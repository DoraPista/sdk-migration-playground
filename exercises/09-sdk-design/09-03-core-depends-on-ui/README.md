# Exercise 09-03 – The Core That Needs a Window

Difficulty: Hard
Estimated Time: 30 minutes

## Skills

- separating core logic from UI and platform
- designing for more than one host
- architecture you can enforce with a test

## Scenario

`MigrationKit.Core` is the heart of the product. It was written alongside the WPF application, and it shows:

- The MAUI team can't use it. Their project won't even reference it.
- A colleague wrote a nightly migration service (a console app) that has to run on a build agent. It crashes
  with `NullReferenceException` before uploading anything.
- Support has a ticket saying a message box titled "Migration" appeared on a machine that runs the app
  unattended overnight, and blocked the migration until someone clicked OK in the morning.

The public contracts everyone agreed on are already in `src/MigrationKit.Core/Contracts.cs`, including
`IThumbnailRenderer`, which the host is supposed to implement (the WPF app can render with WPF, the MAUI app
with its own imaging).

## Your Task

Make `MigrationKit.Core` usable by WPF, MAUI and a plain console application, without changing what a migration does.

## Constraints

- Keep the public API of `MigrationCoordinator` (constructor, `RunAsync`, `ProgressChanged`).
- The WPF demo app must keep working, including its file list and thumbnails.
- Don't add a UI framework or an image library to the core.

## Acceptance Criteria

- The core builds and runs with no UI framework present, and reports progress and per-file outcomes.
- Thumbnails are produced by the renderer the host supplies.
- Nothing in the core shows dialogs.

## How to Run

```bash
dotnet test exercises/09-sdk-design/09-03-core-depends-on-ui/tests
dotnet run --project exercises/09-sdk-design/09-03-core-depends-on-ui/src/MigrationKit.WpfDemo   # Windows only
```

Note: in its current state, one of the tests makes the core pop up a message box. Close it (that is part of the problem).

## When You're Done

All tests pass, the WPF demo still works, and you can explain how you'd stop the core from growing UI dependencies again.
