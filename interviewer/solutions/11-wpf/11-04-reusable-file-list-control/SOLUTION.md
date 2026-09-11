# 11-04 A Control Only One App Can Use – Interviewer Notes

**Type:** WPF component design · **Time:** 30 min · **Solution code:** `code/src/`

## What is wrong

| # | Problem | Why it blocks reuse |
|---|---|---|
| 1 | The constructor calls `MigrationService.Instance` (a static singleton) and `.Result` on its task | The control *fetches* its own data, from *our* app's service, **and** blocks the UI thread (see 02-04) |
| 2 | `ItemsSource`, `SelectedItem`, `ItemActivatedCommand` are plain CLR properties | WPF can't bind to them (no change notification, no binding target). They are decoration |
| 3 | `((MainWindow)Application.Current.MainWindow).ShowDetails(file)` | The control reaches into the application and casts to *our* window type |
| 4 | `MessageBox.Show` for errors | A control must not decide to show UI (see 09-03) |
| 5 | Hard-coded colours and column headers | Can't be themed or relabelled |
| 6 | `SelectedItem` is set in `SelectionChanged`, never read back | Selection can't be driven by the host |

## Hints

1. "The Admin Console has its own data and its own window. Which line stops them using the control?"
2. "Why does binding to `ItemsSource` do nothing? What does WPF need for a bindable property?"
3. "Turn the three properties into dependency properties, bind the inner list to them, and raise a command instead of calling into the app."

## Intended solution

- `DependencyProperty.Register` for `ItemsSource`, `SelectedItem` (with `BindsTwoWayByDefault`) and `ItemActivatedCommand`.
- The inner `ListView` binds to the control's own properties (`ElementName=Root`), so no code-behind plumbing is needed for the selection.
- Double-click executes `ItemActivatedCommand` (with `CanExecute`) and raises an `ItemActivated` event; the control never talks to the app.
- No service, no dialogs; colours from theme resources (or `TemplateBinding`-able properties), headers as properties with defaults.

### Discussion: UserControl vs custom Control

| | `UserControl` (this) | `Control` + default style in `Themes/Generic.xaml` |
|---|---|---|
| Effort | Low | Higher (template parts, `OnApplyTemplate`, `TemplatePart` contracts) |
| Restyling | Host can restyle only what you exposed | Host can replace the whole template |
| Library material | Fine inside one product | The right choice for a control shipped to third parties |

Asking "which would you ship in a control library?" separates people who have only assembled screens from people who have built components.

## Common mistakes

- Making the properties `INotifyPropertyChanged` instead of dependency properties: the *target* of a binding must be a DP.
- Forgetting `BindsTwoWayByDefault` (or `Mode=TwoWay` on the inner binding), so the host's selection never updates.
- Keeping `SelectionChanged` code that writes `SelectedItem` **and** binding it: double bookkeeping, easy to loop.
- Executing the command without checking `CanExecute`.
- Exposing `ObservableCollection<FileItem>` instead of `IEnumerable`, which forces the host's data shape.
- Leaving `FileItem` in the control's contract: the Admin Console has its own row type (the columns bind to `Name`/`Status`, which is a duck-typed contract worth discussing: `DisplayMemberPath`/templates would be more flexible).

## Follow-up questions

- "How would the Admin Console change the columns?" (Expose an `ItemTemplate`/`View` property, or make it a `ListView`-derived control.)
- "Where do the migration app's own behaviours live now?" (In the app's view model and command; the control got smaller.)
- "How do you test a control like this?" (Exactly as these tests do: realise it, drive it, assert observable behaviour.)
- "What if two hosts want different double-click semantics *and* keyboard Enter?" (Input bindings and commands, not events.)

## Scoring notes

| Level | Indicators |
|---|---|
| Weak | Adds INPC to the properties; keeps the service call; passes the window into the control |
| Solid mid-level | Correct dependency properties, two-way selection, command wiring, no service/dialog |
| Strong | Explains DP vs INPC for binding targets, theme resources, `CanExecute`, and keeps the data contract loose |
| Senior | Weighs UserControl vs templated control for a shipped library, discusses the row-type contract and input gestures |
