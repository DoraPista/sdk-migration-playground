# Session D – MAUI (55 minutes)

**Shape:** a list that will not update → navigation and DI done the WPF way → mobile lifecycle → the
shared-core question.
**Reads:** this is the session for a WPF developer moving to MAUI, which is what the role asks for. The
interesting failures are the ones where a desktop habit is *almost* right.

**Prepare:** Windows with the `maui-windows` workload installed (`dotnet workload install maui-windows`).
**Build `exercises/12-maui/MauiGym` the day before** — the first MAUI build is slow enough to ruin a session.
The four exercises overlay one app; only 12-04 has its own test project.

> If the workload is not available, run 12-03 and 16-01 as discussions (no build needed) and fill the rest
> from session C.

---

## 0:00–0:15 · 12-02 The board that doesn't update

**Exercise:** `exercises/12-maui/12-02-migration-list-not-updating/` · Medium · [notes](../solutions/12-maui/12-02-migration-list-not-updating/SOLUTION.md)

**Prompt**

> "Four bug reports about the migration board: the spinner never stops; the list is stale after a refresh;
> the progress simulation does nothing; and it crashes on Android during a refresh. Work through them."

**Hints**

1. "Four symptoms — do not assume one cause. Take them one at a time."
2. "What object is the view bound to after Refresh runs? What would tell it a row changed? Which thread
   mutates the collection?"
3. See the notes.

**Follow-ups**

- "Why does the crash only happen on Android?" (Windows tolerates the off-thread mutation; Android does not.)
- "`ObservableCollection` notifies about *the collection*. What notifies about a row?"
- "Where would you put the `MainThread` call — the view model or the service?"

**Watch for:** whether they resist the urge to find one grand cause; whether they know that replacing a
collection instance requires a property notification; whether they know the main-thread rule differs by
platform. This is the best single exercise for the WPF→MAUI transition.

**Scores:** debugging methodology, correctness, concurrency awareness.

---

## 0:15–0:35 · 12-01 The detail flow a WPF developer wrote

**Exercise:** `exercises/12-maui/12-01-detail-flow-navigation/` · Medium · [notes](../solutions/12-maui/12-01-detail-flow-navigation/SOLUTION.md)

**Prompt**

> "Someone coming from WPF built this detail flow. Four symptoms: the tab bar disappears, back doesn't work,
> the page shows the previous migration's data, and a notification deep link does nothing. Fix the flow."

**Hints**

1. "Which of the four symptoms might share a cause? Start with what happens to the Shell when the page is
   assigned directly."
2. "How does the detail page learn which migration to show, and how would a deep link from a notification
   supply it?"
3. See the notes.

**Follow-ups**

- "What is `Application.Current.Windows[0].Page = …` the WPF equivalent of, and why is it wrong here?"
- "The view model is registered as a singleton. Which symptom is that?"
- "`App.Services.GetService<T>()` inside a page — what is wrong with it, given it works?"
- "How would you test the navigation you just wrote?"

**Watch for:** Shell routes and `IQueryAttributable` rather than a static holding the selection; DI instead
of a service locator; lifetime of the view model. Also whether they explain the *WPF habit* behind each
mistake — that self-awareness is what the role needs.

**Scores:** API design, correctness, maintainability, communication.

---

## 0:35–0:45 · 12-03 Killed in the background

**Exercise:** `exercises/12-maui/12-03-app-killed-in-background/` · Expert Discussion · [notes](../solutions/12-maui/12-03-app-killed-in-background/SOLUTION.md)

Mostly discussion; there is a small code change if time allows.

**Prompt**

> "A field engineer starts a migration on a tablet and switches to email. When they come back, progress has
> stopped and the app has lost its state. The portal says 12 files arrived; the app said 40. Why?"

**Hints**

1. "Write down what this code assumes it will be told before the app goes away."
2. "Which of those callbacks is actually guaranteed when Android or iOS kills a backgrounded process?"

**Follow-ups**

- "The app says 40, the platform says 12. Which is right, and why did they diverge?"
- "So where should a long migration actually run on a tablet?"
- "How do you write the state file so a kill mid-write cannot corrupt it?" (Bridges to 10-03.)

**Watch for:** knowing that there is no guaranteed "you are being killed" callback; not trusting in-memory
progress; reconciling with the platform on restart. A candidate who proposes a background service /
`WorkManager` / letting the server do the work is thinking correctly about the platform.

**Scores:** reasoning, error handling, trade-off awareness.

---

## 0:45–0:55 · 16-01 Design the migration SDK (WPF + MAUI constraint only)

**Exercise:** `exercises/16-system-design/16-01-design-the-migration-sdk/` · Expert Discussion · [notes](../solutions/16-system-design/16-01-design-the-migration-sdk/DISCUSSION.md)

Use only the two-UI part of this exercise; ten minutes is not enough for the whole thing.

**Prompt**

> "One SDK, used by the WPF app and the MAUI app, and later by a headless overnight runner. What does that
> constraint do to the design? What must the SDK never contain?"

**Hints**

1. "What would happen if the SDK called `Dispatcher.Invoke`? What is the MAUI equivalent, and does it exist
   on a server?"
2. "How does progress get to a UI thread if the library doesn't know there is one?"

**Follow-ups**

- "How would you *enforce* that the core has no UI dependency?" (An architecture test — 09-03 does this.)
- "Where do the file picker and the storage folder come from, then?" (Injected abstractions — 12-04.)
- "Does the SDK need to be .NET Standard 2.0? What does that cost you?" (09-04.)

**Watch for:** `IProgress<T>` and `CancellationToken` rather than framework types; platform capabilities
injected rather than referenced; awareness that the headless runner has no main thread at all.

**Scores:** API design, trade-off awareness, maintainability.

---

## Scoring sheet

| Exercise | Dimensions | Hint level | Evidence |
|---|---|---|---|
| 12-02 | debugging, correctness, concurrency | | |
| 12-01 | API design, correctness, maintainability, communication | | |
| 12-03 | reasoning, error handling, trade-offs | | |
| 16-01 | API design, trade-offs, maintainability | | |

**Signals of a solid mid-level session:** treats the four symptoms in 12-02 separately and gets at least
three; uses Shell routes with parameters in 12-01; knows the app can be killed without warning; keeps UI
types out of the shared core.

**Signals above the bar:** names the WPF habit behind each MAUI mistake; proposes an architecture test for
the UI dependency; talks about where the long-running work should live on a mobile device rather than only
how to save state.

**Common false signal:** fluency with MAUI APIs without the threading and lifecycle understanding. Weight
12-02 and 12-03 above 12-01 when they disagree.
