# Session C – WPF (55 minutes)

**Shape:** a silent binding failure → the dispatcher → a leak → making a control reusable.
**Reads:** does this person understand *why* WPF behaves as it does, rather than which incantation usually
works? Every item is something this product's desktop app has actually suffered.

**Prepare:** Windows machine. Build the day before — the WPF test projects take the longest to restore.
Read the four `SOLUTION.md` files. Note that these tests run on an STA thread via `Gym.TestUtilities.Wpf`;
if the candidate is surprised by that, it is worth two minutes of explanation.

---

## 0:00–0:12 · 11-01 The status panel that never updates

**Exercise:** `exercises/11-wpf/11-01-status-panel-not-updating/` · Medium · [notes](../solutions/11-wpf/11-01-status-panel-not-updating/SOLUTION.md)

**Prompt**

> "The migration runs, the log shows files going up, and the status panel shows zero for everything. No
> exceptions, nothing in the output window. The tests reproduce it."

**Hints**

1. "Put a breakpoint in the `FilesUploaded` setter, and another in the panel's constructor. Is the object in
   the setter the one the panel is bound to?"
2. "What does `InitializeComponent()` do to `DataContext`?"
3. See the notes.

**Follow-ups**

- "Why is there no error? What would have told you sooner?" (Binding trace, `PresentationTraceSources`.)
- "There is more than one cause here. Have you found them all?"
- "How would you stop a typo'd property name silently doing nothing?" (`nameof`, `[CallerMemberName]`.)

**Watch for:** whether they know bindings fail silently and where WPF reports them; whether they check the
DataContext identity rather than assuming; whether they find the ordering problem *and* the missing
notifications.

**Scores:** debugging methodology, correctness, maintainability.

---

## 0:12–0:27 · 11-02 Progress from a background migration

**Exercise:** `exercises/11-wpf/11-02-background-migration-progress/` · Medium · [notes](../solutions/11-wpf/11-02-background-migration-progress/SOLUTION.md)

**Prompt**

> "The migration runs on a worker thread and reports each uploaded file. On a small project it works. On a
> real one the app throws, and when it doesn't throw it locks up. Two tests reproduce the two symptoms."

**Hints**

1. "Which thread raises `FileUploaded`, and which thread is allowed to touch `Files`?"
2. "What is the difference between `Invoke` and `BeginInvoke` for the *worker*?"
3. "The UI can't usefully show 5,000 updates a second. What if events were collected and applied a few
   times per second?"

**Follow-ups**

- "Why does this one work in the simple case? What makes the difference?"
- "`Dispatcher.Invoke` fixes the exception. What does it do to the migration's speed?"
- "Where should the marshalling live — in the SDK or in the app?" (The right answer for this product is the
  app; the SDK must not know about `Dispatcher`. Bridges to 09-03 and 16-01.)

**Watch for:** understanding that the exception and the freeze are two different problems; knowing
`Invoke` blocks the worker; reaching coalescing at all. Coalescing is the mid/senior line in this exercise.

**Scores:** concurrency awareness, correctness, trade-off awareness, API design.

---

## 0:27–0:39 · 11-03 The app that grows

**Exercise:** `exercises/11-wpf/11-03-memory-grows-per-migration/` · Medium · [notes](../solutions/11-wpf/11-03-memory-grows-per-migration/SOLUTION.md)

**Prompt**

> "A consultant runs twenty migrations in a day and the app is at 2 GB by the afternoon. Restarting fixes
> it. The test asserts that a closed details view is collected."

**Hints**

1. "After `CloseDetails()`, which objects still hold a reference to the details view model?"
2. "`Dispose` sets `_closed`. Who reads it?"
3. See the notes.

**Follow-ups**

- "How would you have found this without the test?" (A memory profiler and two snapshots; know the workflow.)
- "Whose responsibility is unsubscribing — the publisher or the subscriber?"
- "What about the `DispatcherTimer`? Why is it different from a normal field?"
- "Would weak events solve this? Should they?"

**Watch for:** knowing that an event subscription is a reference *from* the publisher; noticing the timer as
a separate root; whether `Dispose` being written but never called is spotted. A candidate who says "I'd take
a snapshot before and after and diff the object counts" is describing exactly the right method.

**Scores:** debugging methodology, correctness, maintainability.

---

## 0:39–0:55 · 11-04 A control only one app can use

**Exercise:** `exercises/11-wpf/11-04-reusable-file-list-control/` · Medium · [notes](../solutions/11-wpf/11-04-reusable-file-list-control/SOLUTION.md)

**Prompt**

> "This file-list control works in the migration app. The Admin Console team want to use it and can't. Make
> it reusable — the tests describe what they need."

**Hints**

1. "The Admin Console has its own data and its own window. Which line stops them using the control?"
2. "Why does binding to `ItemsSource` do nothing? What does WPF need for a bindable property?"
3. See the notes.

**Follow-ups**

- "When is a `DependencyProperty` the right choice and when is `INotifyPropertyChanged` enough?"
- "Should the control raise an event or expose a command? What does each cost the consumer?"
- "The Admin Console wants a different row layout. What would you have to change?" (Templating.)

**Watch for:** whether they know why plain CLR properties are not bindable targets; whether the control ends
up depending on the app's types or on nothing; whether they think about the second consumer's needs, which
is the same skill as 09-01 in a smaller frame.

**Alternative for this slot** (discussion rather than code, if the candidate is not a WPF specialist and you
want reasoning instead): `exercises/15-code-review/15-02-review-migration-window/` — same 16 minutes, notes
[here](../solutions/15-code-review/15-02-review-migration-window/SOLUTION.md).

**Scores:** API design, maintainability, code quality.

---

## Scoring sheet

| Exercise | Dimensions | Hint level | Evidence |
|---|---|---|---|
| 11-01 | debugging, correctness, maintainability | | |
| 11-02 | concurrency, correctness, trade-offs, API design | | |
| 11-03 | debugging, correctness, maintainability | | |
| 11-04 | API design, maintainability, code quality | | |

**Signals of a solid mid-level session:** knows bindings fail silently and where to see them; fixes the
cross-thread problem without blocking the worker; identifies both roots of the leak; turns the control's
properties into dependency properties and removes the dependency on the app.

**Signals above the bar:** reaches coalescing in 11-02 and can say what latency it costs; argues about where
marshalling belongs (SDK vs app); proposes templating in 11-04 before being asked.

**Common false signal:** a candidate who has memorised "always use `Dispatcher.Invoke`" will pass 11-02's
first test and fail its second. Ask about throughput before you judge.
