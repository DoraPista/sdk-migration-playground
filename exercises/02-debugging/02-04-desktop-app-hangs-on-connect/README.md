# Exercise 02-04 – The Desktop App Hangs on "Connect"

Difficulty: Medium
Estimated Time: 20 minutes

## Skills

- async/await and synchronization contexts
- sync-over-async
- SDK code consumed by UI applications

## Scenario

The SDK ships a `MigrationClient`. It works in the console test harness and in the integration tests.

In the customer's WPF application:

- Clicking **Connect** freezes the window. It never recovers, and the user has to kill the process.
- A background timer that calls `CheckHealth()` from the UI thread freezes the app in the same way.
- With the network cable unplugged, the app doesn't show its "Cannot reach the migration service" message.
  It crashes. The app's handler catches `HttpRequestException`.

The WPF team calls the synchronous `Connect()` from about 40 places and can't move everything to async
this release. The async API also exists and is used by newer screens.

## Your Task

Find out why the app hangs and crashes, and fix the SDK so that both the synchronous and the asynchronous
APIs behave when called from a UI thread.

## Constraints

- Keep `Connect()` and `CheckHealth()` for now.
- You can't change the WPF application.

## Acceptance Criteria

- `Connect()` and `CheckHealth()` complete when called from a UI thread.
- Awaiting `ConnectAsync()` from a UI thread still resumes on the UI thread afterwards.
- Connection failures surface as the exception the caller expects.

## How to Run

```bash
dotnet test exercises/02-debugging/02-04-desktop-app-hangs-on-connect/tests
```

The tests use a simulated single-threaded UI thread (`Gym.TestUtilities.UiThread`) that behaves like
the WPF dispatcher.

## When You're Done

All tests pass, and you can explain exactly which thread is waiting for which.
