# 12-02 The Board That Doesn't Update – Interviewer Notes

**Type:** MAUI debugging (binding + threading) · **Time:** 20 min · **Solution code:** `code/` (overlays `MauiGym`)

## Hints

1. "Four symptoms - do not assume one cause. Take them one at a time."
2. "What object is the view bound to after Refresh runs? What would tell it a row changed? Which thread mutates the collection?"
3. "Keep one ObservableCollection and clear/add into it, make the row implement INotifyPropertyChanged, mutate on the main thread, and reset IsRefreshing in a finally."

## The three different reasons a CollectionView "doesn't update"

| Symptom | Cause | Fix |
|---|---|---|
| 2. List empty / stale after refresh | The view model **replaces** `Migrations` with a new `ObservableCollection` and never raises `PropertyChanged` for the property. The view is still bound to the old instance | Create the collection once, then `Clear()`/`Add()` (or raise the property change) |
| 3. *Simulate progress* does nothing | `MigrationRow` has no `INotifyPropertyChanged`. The collection didn't change, so the CollectionView has no reason to re-read the rows | Make the row notify |
| 4. Crash on Android during refresh | The collection is modified inside `Task.Run`, i.e. off the main thread. Windows tolerates it; Android throws | `MainThread.InvokeOnMainThreadAsync` for the UI-visible part |
| 1. Spinner never stops | `IsRefreshing` is set to `true` and never back to `false` | Reset in a `finally`, on the main thread |

That table is the exercise: three distinct mechanisms that all look like "binding is broken".

## Extra points

- `Task.Run` around an `await`ed HTTP call is pointless: async I/O doesn't need a thread. Removing it is both faster and safer.
- `RefreshView.IsRefreshing` is two-way bound: the control sets it to `true` when the user pulls, and the view model must set it back.
- A re-entrancy guard (`if (IsRefreshing) return;`) prevents overlapping refreshes.
- `ObservableCollection.Clear()` raises a Reset, which makes the CollectionView rebuild everything; for large lists, updating rows in place or using an ObservableRangeCollection is nicer. Worth asking about with 5,000 rows.

## Common mistakes

- Adding `MainThread.BeginInvokeOnMainThread` around everything, including the HTTP call.
- Raising `PropertyChanged` for `Migrations` but still replacing it (works, and wastes the view's item containers).
- Making the row a `record` with `init` properties and wondering why the UI doesn't update.
- Setting `IsRefreshing = false` only on the success path.

## Follow-up questions

- "The SDK raises progress events from a worker thread. Where does the marshalling belong?" (In the app/view model, not the SDK; see 11-02 and 09-03.)
- "What's the MAUI equivalent of WPF's `Dispatcher`?" (`MainThread`, or `IDispatcher`/`Dispatcher` on any `BindableObject`; `IDispatcher` is injectable and therefore testable.)
- "How would you unit-test this view model?" (Inject `IDispatcher`, or extract the non-UI logic; see 12-04.)

## Scoring notes

| Level | Indicators |
|---|---|
| Weak | Fixes one symptom; adds `MainThread` everywhere; doesn't see the replaced collection |
| Solid mid-level | All four causes named and fixed; `finally` for the spinner |
| Strong | Removes the needless `Task.Run`; adds re-entrancy protection; knows why Windows didn't crash but Android does |
| Senior | Discusses collection-change costs at scale, dispatcher injection for testability, and where marshalling belongs across SDK/app |
