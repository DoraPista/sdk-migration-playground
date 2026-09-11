# Exercise 12-04 – A View Model You Cannot Test

Difficulty: Medium
Estimated Time: 30 minutes

## Skills

- separating platform APIs from logic
- testability in MAUI
- platform differences without `#if`

## Scenario

`ExportViewModel` drives the "export to the migration platform" screen of the MAUI app. QA found two bugs in it
last month, and both times the team could only reproduce them by hand on a device, because the view model
cannot be instantiated in a test: it calls MAUI's platform APIs directly (file picker, connectivity,
preferences, alerts, main thread), and those only work inside a running app.

Someone already added the abstractions the team agreed on to the constructor (`IFilePickerService`,
`IConnectivityService`, `IUserSettings`, `IDialogService`, `IUiThread`, `DeviceCapabilities`).
Nothing uses them.

`PlatformAccess` in this project stands in for MAUI's statics (`FilePicker.Default`, `Connectivity.Current`,
`Preferences.Default`, `DisplayAlert`, `MainThread`): like the real ones, it throws outside the app.

## Your Task

Make `ExportViewModel` work through the abstractions so that its behaviour can be tested, without changing what
the screen does.

## Constraints

- Keep the public API of `ExportViewModel` (constructor parameters, methods, properties).
- The platform-specific upload concurrency must keep its values (Windows 8, mobile 2), but it must not be decided with `#if`.
- No MAUI types in this project: the app supplies the implementations.

## Acceptance Criteria

- Every test in `tests/` passes; none of them runs inside a MAUI app.
- The view model contains no `PlatformAccess` calls and no `#if` platform blocks.

## How to Run

```bash
dotnet test exercises/12-maui/12-04-platform-code-in-viewmodel/tests
```

## When You're Done

You can explain what the MAUI app's `MauiProgram` would register for each abstraction, and where the remaining
platform-specific code should live.
