# Exercise 11-04 – A Control Only One App Can Use

Difficulty: Medium
Estimated Time: 30 minutes

## Skills

- reusable WPF controls
- dependency properties and bindings
- commands instead of call-backs into the app

## Scenario

`FileListControl` shows the files of a migration. It works well in the migration app.

The **Admin Console** team wants to reuse it: same look, same behaviour, different data (files of *any* customer,
loaded by their own view model) and a different action when a row is activated (they open an audit view, not our details window).

They tried, and reported:

- Creating the control throws (`MigrationService has not been initialised`); their app doesn't use our migration service.
- `ItemsSource`, `SelectedItem` and `ItemActivatedCommand` exist as properties, but binding to them does nothing.
- Double-clicking a row throws, because the control casts `Application.Current.MainWindow` to *our* main window.
- Errors pop up a `MessageBox` from inside the control, which their app is not allowed to do (it runs unattended in kiosk mode).

## Your Task

Make the control reusable by any WPF application.

## Constraints

- Keep the type name `FileListControl` and the property names `ItemsSource`, `SelectedItem`, `ItemActivatedCommand`.
- Our own app must keep working the same way (it can supply the data and the command).
- Double-clicking a row must execute `ItemActivatedCommand` with that row's item as the parameter.

## Acceptance Criteria

- The control can be created and used with no migration service, no `Application.Current`, and no dialogs.
- `ItemsSource` shows what it is given, `SelectedItem` works in both directions, and `ItemActivatedCommand` runs on double-click.

## How to Run

```bash
dotnet test exercises/11-wpf/11-04-reusable-file-list-control/tests
```

## When You're Done

All tests pass, and you can explain when you would build a `UserControl` versus a custom `Control` with a default style.
