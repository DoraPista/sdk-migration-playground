# 11-01 The Status Panel That Never Updates – Interviewer Notes

**Type:** WPF debugging · **Time:** 20 min · **Solution code:** `code/src/`

Four bugs, layered so that fixing one reveals the next. Watch how the candidate **diagnoses**: the Output window's
binding errors and a breakpoint in the setter beat guessing.

| # | Bug | Effect | How to spot it |
|---|---|---|---|
| 1 | `<UserControl.DataContext><vm:MigrationStatusViewModel/></UserControl.DataContext>` in XAML | `InitializeComponent()` runs **after** `DataContext = viewModel` in the constructor and replaces it. The panel binds to a second, empty view model that nobody updates | The binding error names a `MigrationStatusViewModel` with a *HashCode* that isn't the app's instance; or check `panel.DataContext` in the debugger |
| 2 | `OnPropertyChanged("FilesUploded")` | A silent typo: WPF raises a change for a property that doesn't exist, so the `FilesUploaded` binding never refreshes | The binding error in the README ("property not found") is the same class of problem; `nameof` prevents it |
| 3 | `StatusText` is computed from `State`, but nothing raises `PropertyChanged` for it | The status line never changes | Setter review: which properties depend on this one? |
| 4 | `Value="{Binding Percent, Mode=OneTime}"` | The progress bar reads the value once, at binding time (0), and never again | Look at the binding's Mode |

**A trap inside the trap:** simply deleting `Mode=OneTime` does not work either. `ProgressBar.Value` (`RangeBase.Value`) binds **two-way by default**, and `Percent` is read-only, so the binding throws
`InvalidOperationException: A TwoWay or OneWayToSource binding cannot work on the read-only property`. It has to be `Mode=OneWay`. Candidates who know the default-mode rule spot this immediately; others see the XamlParseException and work back from it.

## Hints

1. "Put a breakpoint in the `FilesUploaded` setter, and another in the panel's constructor. Is the object in the setter the one the panel is bound to?"
2. "What does `InitializeComponent()` do to `DataContext`?"
3. "For each property the UI shows: who raises `PropertyChanged` for it, and with what name?"

## Intended solution

- Remove the XAML `DataContext` (keep a `d:DataContext` design-time instance for IntelliSense, which is why people add the real one in the first place).
- `nameof` / `[CallerMemberName]` everywhere.
- Raise `PropertyChanged` for computed properties (`Percent`, `StatusText`) when their inputs change.
- Binding mode back to the default (OneWay for a `ProgressBar.Value` bound to a read-only property).

## Alternatives worth discussing

- A source generator (`CommunityToolkit.Mvvm`'s `[ObservableProperty]` + `[NotifyPropertyChangedFor]`) removes bugs 2 and 3 by construction. Ask whether they'd adopt it.
- `DependencyProperty`-based controls instead of INPC for the view's own state.
- Setting `DataContext` from the *parent* (`<local:MigrationStatusPanel DataContext="{Binding CurrentMigration}" />`) rather than passing it into the constructor, which also fixes bug 1 and is more idiomatic in MVVM.
- Turning binding errors into failures during development: `PresentationTraceSources.DataBindingSource` with a listener that throws, or the `[DebuggerDisplay]`/`d:DataContext` combination to catch typos early.

## Common mistakes

- Moving `DataContext = viewModel` after `InitializeComponent()` — correct, and worth noting that it **also** leaves the XAML-created instance alive for a moment. Removing the XAML element is cleaner.
- Fixing the typo with another string literal.
- Raising `PropertyChanged(null)` everywhere ("refresh everything"): works, but wasteful and hides intent. Discuss.
- Changing `Percent` into a settable property maintained by hand.

## Follow-up questions

- "How would you have caught these in review?" (nameof/analyzers, no logic in XAML-created DataContexts, binding-error listener in debug builds.)
- "The migration updates `FilesUploaded` from a background thread. Does INPC work?" (For scalar properties WPF marshals; collections do not: see 11-02.)
- "Would you unit-test bindings in a real project?" (Usually not per binding: a smoke test that realises the view catches the big ones, which is what these tests do.)

## Scoring notes

| Level | Indicators |
|---|---|
| Weak | Rewrites the view model; doesn't find the DataContext bug; can't read the binding error |
| Solid mid-level | Finds all four, explains the InitializeComponent ordering, uses nameof |
| Strong | Explains computed-property notification, binding modes, and how to prevent the class of bug (analyzers/toolkit/design-time context) |
| Senior | Talks about diagnosing binding failures in production builds, and MVVM structure that avoids constructor-injected DataContexts |
