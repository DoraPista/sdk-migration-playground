# .NET MAUI exercises

Three of the four MAUI exercises live in one app, `MauiGym`, with one tab per exercise.
The fourth (12-04) is a separate, testable project, because that exercise is about making MAUI code testable.

## Running the app

```bash
# Once:
dotnet workload install maui-windows

dotnet build exercises/12-maui/MauiGym
dotnet run --project exercises/12-maui/MauiGym -f net10.0-windows10.0.19041.0
```

The app targets Windows only so that the `maui-windows` workload is enough. To try it on Android or iOS, add those
target frameworks to `MauiGym.csproj` and install the matching workloads.

`MauiGym` is deliberately **not** part of `InterviewGym.slnx`: MAUI needs its own workload, and the rest of the gym
should build without it.

## The exercises

| Exercise | Where the code is |
|---|---|
| [12-01 Detail flow](12-01-detail-flow-navigation/) | `MauiGym/Exercises/E1201DetailFlow/`, `MauiProgram.cs`, `AppShell.xaml.cs` |
| [12-02 The board that doesn't update](12-02-migration-list-not-updating/) | `MauiGym/Exercises/E1202MigrationList/` |
| [12-03 Killed in the background](12-03-app-killed-in-background/) | `MauiGym/Exercises/E1203Lifecycle/`, `App.xaml.cs` |
| [12-04 Platform code in the view model](12-04-platform-code-in-viewmodel/) | its own `src/` and `tests/` |

## A note for a WPF developer

MAUI looks like WPF and is not WPF. The things that bite hardest:

- **Navigation** is Shell-based (routes, query parameters), not "replace the window's content".
- **The process can be killed** while your app is in the background. There is no guaranteed "closing" event.
- **The UI thread** is reached with `MainThread`, not `Dispatcher` (though `IDispatcher` exists too).
- **Bindings** are compiled (`x:DataType`); a typo is a build error rather than a silent no-op, if you use them.
- **Platform APIs** (picker, connectivity, preferences) are static singletons that do nothing in unit tests.
