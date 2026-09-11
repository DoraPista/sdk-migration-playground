# Exercise 12-02 – The Board That Doesn't Update

Difficulty: Medium
Estimated Time: 20 minutes

## Skills

- `CollectionView` and data binding in MAUI
- change notification for rows
- the MAUI main thread
- `RefreshView`

## Scenario

The board (tab *12-02 Board*) shows all migrations of a customer. Testers report:

1. Pull-to-refresh spins **forever**, even though the data is fetched.
2. After a refresh the list is empty (or still shows the old rows), although the fetch returned four migrations.
3. When you press *Simulate progress* (which stands in for progress events from the SDK), nothing changes on screen.
4. On Android the app sometimes crashes during a refresh with
   `Only the original thread that created a view hierarchy can touch its views.`

## Your Task

Make the board show the data, update while the migration runs, and stop spinning.

The code is in `MauiGym/Exercises/E1202MigrationList/`.

## Constraints

- Keep `CollectionView` and `RefreshView`.
- The API call itself must stay off the UI thread.

## Acceptance Criteria

- Refresh loads the rows, shows them, and stops the spinner.
- Row changes (state, file count) appear without a full refresh.
- Nothing touches UI state from a background thread.

## How to Run

The app is `exercises/12-maui/MauiGym`. Build and run it on Windows:

```bash
dotnet build exercises/12-maui/MauiGym
dotnet run --project exercises/12-maui/MauiGym -f net10.0-windows10.0.19041.0
```

Requires the `maui-windows` workload (`dotnet workload install maui-windows`).

## When You're Done

You can explain the three different reasons a `CollectionView` "doesn't update", and which one applied to each symptom.
