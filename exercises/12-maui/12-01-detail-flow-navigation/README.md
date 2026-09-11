# Exercise 12-01 – The Detail Flow a WPF Developer Wrote

Difficulty: Medium
Estimated Time: 30 minutes

## Skills

- MAUI Shell navigation
- passing data between pages
- dependency injection in MAUI
- spotting WPF habits that don't fit

## Scenario

The migration list and its detail page were ported from the WPF app by a developer who knew WPF well and
MAUI not at all. It "works" on Windows. The testers found:

1. On Android, the hardware **Back** button closes the app instead of returning to the list.
2. The navigation bar has no back arrow, and the tab bar disappears once you open a migration.
3. Opening a second migration briefly shows the **previous** migration's data.
4. Deep links (`//migrations/detail?id=mig-00102`) were part of the plan; nobody can make them work.
5. The detail page is created with `new`, so it fetches its services from a static `App.Services`.

Run the app and try it (tab *12-01 Detail flow*):

```bash
dotnet run --project exercises/12-maui/MauiGym -f net10.0-windows10.0.19041.0
```

## Your Task

Rework the detail flow so it uses MAUI's navigation properly, and explain each change.

The code is in `MauiGym/Exercises/E1201DetailFlow/`, plus the registrations in `MauiProgram.cs`
and `AppShell.xaml.cs`.

## Constraints

- Keep `IMigrationApi` as the way data is fetched.
- The list must stay inside the tab bar; the detail page is pushed on top of it.

## Acceptance Criteria

- Navigation uses Shell (routes and navigation parameters); the platform back gesture and back button work.
- Pages and view models come from dependency injection, not from a static service locator or shared static state.
- Opening a migration never shows another migration's data.

## How to Run

The app is `exercises/12-maui/MauiGym`. Build and run it on Windows:

```bash
dotnet build exercises/12-maui/MauiGym
dotnet run --project exercises/12-maui/MauiGym -f net10.0-windows10.0.19041.0
```

Requires the `maui-windows` workload (`dotnet workload install maui-windows`).

## When You're Done

You can explain how Shell navigation differs from what the WPF app did, and what `AppState` should be replaced with.
