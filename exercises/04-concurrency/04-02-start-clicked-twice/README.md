# Exercise 04-02 – "Start Migration" Clicked Twice

Difficulty: Medium
Estimated Time: 25 minutes

## Skills

- check-then-act races
- idempotent operations
- sharing an in-flight operation between callers

## Scenario

On a slow connection, creating a migration on the platform takes a few seconds. Users click
**Start Migration**, nothing seems to happen, so they click again.

Result: two migrations are created on the platform for the same customer, and every file is uploaded twice,
once into each migration. The WPF and MAUI apps both call `MigrationController.StartAsync` directly from the button's command.

A developer already tried to handle this ("if a migration is running, return it") but the problem remains.

## Your Task

Make `MigrationController` safe against duplicate starts.

Ask whatever clarifying questions you think you need.

## Constraints

- Keep the public API of `MigrationController`.
- Disabling the button in the UI is welcome, but not enough on its own: the SDK is also called from
  scripts and from other apps, and the UI can't disable the button until the first click has been handled.

## Acceptance Criteria

- Starting while a migration is being started or is running returns the migration that is already in progress,
  and creates nothing new.
- Once a migration has finished, a new one can be started.
- If starting fails, the user can simply try again.

## How to Run

```bash
dotnet test exercises/04-concurrency/04-02-start-clicked-twice/tests
```

## When You're Done

All tests pass, and you have an opinion about what should happen if **two app instances** (or two machines) start the same migration.
