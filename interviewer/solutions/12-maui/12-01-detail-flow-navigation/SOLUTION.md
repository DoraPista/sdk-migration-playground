# 12-01 The Detail Flow a WPF Developer Wrote – Interviewer Notes

**Type:** MAUI refactoring · **Time:** 30 min · **Solution code:** `code/` (overlays `MauiGym`)

## Hints

1. "Which of the four symptoms might share a cause? Start with what happens to the Shell when the page is assigned directly."
2. "How does the detail page learn which migration to show, and how would a deep link from a notification supply it?"
3. "Shell routes with query parameters (IQueryAttributable), pages resolved from DI, and a transient detail view model instead of a singleton."

## What is wrong

| # | Code | Why it's a WPF habit |
|---|---|---|
| 1 | `LegacyNavigationService` sets `Application.Current.Windows[0].Page = new NavigationPage(new MigrationDetailPage())` | That is "replace the window's content", as WPF does with `MainWindow.Content`. It **throws away the Shell**, so the tab bar, the navigation stack and the platform back behaviour go with it (symptoms 1 and 2) |
| 2 | `AppState.SelectedMigration` (a static) carries the selection | Global mutable state instead of navigation parameters. Deep links can't work (symptom 4), and two flows would fight over it |
| 3 | `MigrationDetailPage` is created with `new` and pulls services from `App.Services` | Shell can create pages from the DI container; the service locator hides dependencies and makes the page untestable |
| 4 | `MigrationDetailViewModel` is registered as a **singleton** | It keeps the previous migration's data, which is symptom 3 |
| 5 | `Routing.RegisterRoute("detail", …)` exists but nothing navigates to it | The route was registered and never used |
| 6 | `OnAppearing` is `async void` and loads data every time | Works, but `OnAppearing` fires on every return to the page; a strong candidate mentions it |

## Intended solution (see `code/`)

- `Routing.RegisterRoute("detail", typeof(MigrationDetailPage))` stays; navigation becomes
  `await Shell.Current.GoToAsync("detail", new Dictionary<string, object> { ["Migration"] = migration })`.
- `MigrationDetailViewModel` implements `IQueryAttributable` (or the page uses `[QueryProperty]`) to receive the migration.
- Pages and view models are registered **transient** and injected through constructors; `App.Services` disappears.
- Back navigation is whatever the platform provides (`GoToAsync("..")` when needed); nothing replaces the root page.

## Discussion points

- **Shell vs NavigationPage**: Shell gives routes, deep links, flyout/tabs and back behaviour. A plain `NavigationPage` stack is fine for simple apps; mixing the two (as here) is what breaks.
- **Passing objects vs ids**: passing the whole `MigrationSummary` object is convenient but doesn't survive a real deep link or process death. Passing `id` and re-fetching is more robust. Ask which they'd choose and why.
- **`ContentTemplate` vs registered routes**: `ShellContent ContentTemplate="{DataTemplate …}"` creates pages lazily; with DI registration, MAUI resolves them from the container.
- **Where does `IQueryAttributable` put you?** It runs before `OnAppearing`; loading data there is the usual pattern.
- **Process death**: on Android the app can be recreated from scratch at the detail page. Static `AppState` is empty then (connects to 12-03).

## Common mistakes

- Replacing the static with a "NavigationParameters" singleton (same problem, new name).
- Using `Shell.Current.Navigation.PushAsync(new MigrationDetailPage())`: works, but skips routes and DI.
- Registering pages as singletons ("faster"), which keeps stale view models.
- Keeping `App.Services` "just for this one page".

## Scoring notes

| Level | Indicators |
|---|---|
| Weak | Adds a back button that swaps pages again; keeps the static state; can't say why Android's back button exits |
| Solid mid-level | Shell routes with parameters, DI-created pages, transient view models, no statics |
| Strong | Explains ids vs objects for deep links and process death; `IQueryAttributable` timing; keeps the tab bar |
| Senior | Talks about navigation as an app-wide concern (testable navigation service over Shell), deep-link strategy, and state restoration |
