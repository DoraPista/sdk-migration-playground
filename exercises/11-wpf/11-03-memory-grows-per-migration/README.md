# Exercise 11-03 – The App That Grows

Difficulty: Medium
Estimated Time: 20 minutes

## Skills

- object lifetime in WPF applications
- event subscriptions and leaks
- diagnosing with evidence

## Scenario

Consultants keep the desktop app open all week and run dozens of migrations. Support has two reports:

1. "By Thursday the app uses 4 GB and everything is slow. Restarting it fixes it."
2. "While a new migration runs, the *old* details windows seem to still be doing something: the app gets
   slower with every migration we've opened, and the log shows progress lines for migrations that finished days ago."

`MigrationMonitor` is created once at start-up and lives for the life of the application. The shell opens a
details view per migration and closes it when the user clicks away.

## Your Task

Find out why closed details views stay alive, and fix it.

## Constraints

- Keep the public API of `ShellViewModel` and `MigrationDetailsViewModel`.
- The details view must keep updating while it is open.

## Acceptance Criteria

- After closing a details view, it no longer reacts to monitor events and no longer has a running timer.
- A closed details view can be garbage-collected.
- Opening and closing many details views leaves no subscriptions behind.

## How to Run

```bash
dotnet test exercises/11-wpf/11-03-memory-grows-per-migration/tests
```

## When You're Done

All tests pass, and you can describe how you would find this leak in a memory dump from a customer.
