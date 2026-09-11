# 12-04 A View Model You Cannot Test – Interviewer Notes

**Type:** MAUI refactoring for testability · **Time:** 30 min · **Solution code:** `code/src/ExportViewModel.cs`

## Hints

1. "Try to write one test for the export flow. What stops you?"
2. "The constructor already takes the abstractions it needs. What does the body use instead?"
3. "Use the injected picker, connectivity, preferences, alert and main-thread abstractions everywhere, and turn the #if WINDOWS concurrency into an injected option."

## What is wrong

- Every platform capability is reached through a **static**: `PlatformAccess.PickFilesAsync/IsOnline/GetPreference/ShowAlertAsync/OnMainThread`
  (standing in for `FilePicker.Default`, `Connectivity.Current`, `Preferences.Default`, `DisplayAlert`, `MainThread`).
  Statics can't be replaced in a test, which is why QA had to reproduce everything on a device.
- The abstractions are **injected and ignored**: the constructor takes six of them and uses one.
- `#if WINDOWS` decides the upload concurrency **inside the logic**, so the behaviour differs per build and can't be tested per case.
- `LastFolder` reads preferences in a property getter (a throw waiting to happen in a designer or a test).

## Intended solution

Use the injected abstractions, delete `PlatformAccess` usage, and take the concurrency from `DeviceCapabilities`.
Nothing else about the screen changes.

## What the MAUI app then registers (the follow-up question)

```csharp
builder.Services.AddSingleton<IFilePickerService>(_ => new MauiFilePicker(FilePicker.Default));
builder.Services.AddSingleton<IConnectivityService>(_ => new MauiConnectivity(Connectivity.Current));
builder.Services.AddSingleton<IUserSettings>(_ => new MauiPreferences(Preferences.Default));
builder.Services.AddSingleton<IDialogService, MauiDialogService>();       // uses Shell.Current.DisplayAlertAsync
builder.Services.AddSingleton<IUiThread, MauiUiThread>();                 // MainThread.BeginInvokeOnMainThread
builder.Services.AddSingleton(new DeviceCapabilities
{
    MaxParallelUploads = DeviceInfo.Platform == DevicePlatform.WinUI ? 8 : 2,
});
```

Each adapter is a handful of lines and has no logic, so it needs no tests of its own. Note that MAUI's Essentials
types already have interfaces (`IFilePicker`, `IConnectivity`, `IPreferences`): injecting those directly is a
legitimate alternative that skips the adapters, at the cost of a MAUI dependency in the view-model layer.
Ask which they prefer and why: the answer says a lot about how they think about dependencies.

## Where platform-specific code should live

- Values (like concurrency) → composition root, as data.
- Behaviour that genuinely differs → a platform implementation of an interface, in `Platforms/<platform>/` or behind
  MAUI's partial-class pattern; not `#if` in shared logic.
- Permissions (`Permissions.RequestAsync<…>`) → behind an abstraction too; the view model asks "may I?", it doesn't
  know about Android's runtime permissions or iOS's usage descriptions.

## Common mistakes

- Keeping `PlatformAccess` and adding a `SetForTesting(...)` static hook (a service locator with extra steps).
- Replacing `#if WINDOWS` with `DeviceInfo.Platform == …` inside the view model (still untestable and still platform code in logic).
- Injecting but then calling `MainThread.BeginInvokeOnMainThread` directly "because it's simple".
- Making `LastFolder` a property that still hits settings on every get, then wondering why the designer throws. (A field set at construction, or an explicit `LoadAsync`, is nicer.)

## Scoring notes

| Level | Indicators |
|---|---|
| Weak | Adds test hooks to the statics; leaves `#if`; can't explain why the tests couldn't run |
| Solid mid-level | Uses every injected abstraction, removes the statics and the `#if`, all tests pass |
| Strong | Explains where platform decisions belong; adapters are thin; mentions permissions and the designer/test-time hazards |
| Senior | Compares own abstractions vs MAUI's Essentials interfaces, discusses the cost of abstraction layers, and how this shapes SDK boundaries (see 09-03) |
