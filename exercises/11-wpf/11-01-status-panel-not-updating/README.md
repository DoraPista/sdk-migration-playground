# Exercise 11-01 – The Status Panel That Never Updates

Difficulty: Medium
Estimated Time: 20 minutes

## Skills

- WPF data binding
- `INotifyPropertyChanged`
- DataContext and where it comes from

## Scenario

The migration status panel shows: files uploaded, a status line, and a progress bar. In the running app it
shows `0`, `Idle` and an empty progress bar, and it stays that way for the whole migration. The migration
itself works: the portal fills up, and the log shows progress.

A developer added a couple of "quick fixes" last week ("it worked on my branch"), and the panel is now
worse than before, but nobody can say exactly what changed.

Output window when the app runs:

```
System.Windows.Data Error: 40 : BindingExpression path error: 'StatusText' property not found on 'object' ''MigrationStatusViewModel' (HashCode=41320073)'.
```

(The property *is* there.)

## Your Task

Make the panel show what the view model says, as it changes.

## Constraints

- Keep the public API of `MigrationStatusViewModel` and the constructor of `MigrationStatusPanel`.
- The panel must show the view model instance it is given (the app creates one per migration).

## Acceptance Criteria

- The panel binds to the view model it was constructed with.
- Uploaded count, status text and progress bar all follow the view model while a migration runs.

## How to Run

```bash
dotnet test exercises/11-wpf/11-01-status-panel-not-updating/tests
```

The tests drive real WPF controls on an STA thread, so they run on Windows only.

## When You're Done

All tests pass, and you can explain that binding error message. (There is more than one bug.)
