# Exercise 12-03 – Killed in the Background

Difficulty: Expert Discussion
Estimated Time: 20 minutes

## Skills

- mobile application lifecycle
- assumptions that hold on Windows and not on phones
- durable progress

## Scenario

Site engineers start a migration on a tablet and then switch to their email, take a photo, or simply put the
device down. Reports from the field:

1. "It was at 60% and when I came back it started again from zero."
2. "It says 40 files uploaded, the portal has 12."
3. On Windows, closing the window mid-migration works fine. On Android, nothing is saved at all.

The code (tab *12-03 Background*) assumes the app keeps running until the user closes it, and writes its
state when the window is destroyed (`App.xaml.cs`).

## Your Task

1. List the assumptions in `MigrationRunner` and `App.CreateWindow` that do not hold on a phone or tablet.
2. Change the runner so progress survives the app being killed at any moment.
3. Explain what would be needed for the migration to *continue* while the app is in the background on Android and on iOS,
   and what you would recommend to product.

The code is in `MauiGym/Exercises/E1203Lifecycle/` and `App.xaml.cs`.

## Acceptance Criteria

- Progress is durable without relying on a shutdown notification.
- Restarting the app resumes from what was actually uploaded.

## Constraints

- You cannot stop the operating system killing a backgrounded app.
- The field team work on tablets over 4G and switch apps constantly.
- The platform is the only place that knows what actually arrived.

## How to Run

The app is `exercises/12-maui/MauiGym`. Build and run it on Windows:

```bash
dotnet build exercises/12-maui/MauiGym
dotnet run --project exercises/12-maui/MauiGym -f net10.0-windows10.0.19041.0
```

Requires the `maui-windows` workload (`dotnet workload install maui-windows`).

## When You're Done

You can explain the MAUI lifecycle events, which ones are guaranteed, and what background execution really costs on each platform.
