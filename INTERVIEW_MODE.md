# Interview Mode

Five complete mock interview sessions built from the exercises in this repository. Each one is a realistic
interview for the target role, not a tour of the repository.

This file is **candidate-safe**: it contains the running order, the timings and what each session is looking
for, and no answers. The interviewer's copy of each session — with prompts, hints, scoring sheets and what to
watch for — is in [`interviewer/mock-interviews/`](interviewer/mock-interviews/).

## Before the session

**Interviewer**

1. `dotnet build InterviewGym.slnx` on the machine that will be used, the day before. A first build that
   restores packages takes minutes and eats interview time.
2. Read the session script in `interviewer/mock-interviews/`, and the `SOLUTION.md` of each exercise in it.
3. Reset the exercises: `git checkout -- exercises/` (or use a fresh clone).
4. Decide who drives. Candidate-driven is better; be ready to take over if the tooling gets in the way.

**Candidate**

1. Check `dotnet --version` reports 10.x, and that `dotnet test` runs on one exercise.
2. Have an editor you are comfortable in. You will be reading more than writing.
3. You may look things up. Say what you are looking for and why.

## How to run one

- **Think out loud.** Silence is the one thing that cannot be scored. Say what you suspect, what you would
  check, and what you would do if you had more time.
- **The tests are the specification.** Most exercises start red on purpose; read the failing test first.
- **Finishing is not the goal.** Two exercises with good reasoning beat four rushed fixes. The interviewer
  will move you on; being moved on is not a failure signal.
- **Hints are part of the format.** Three levels exist for every exercise. Asking for one costs less than
  fifteen silent minutes.
- **Say what you would do differently in real life** — more logging, a profiler, a colleague, a spike.
  That sentence is worth as much as the fix.

## The five sessions

| | Session | Length | Shape |
|---|---|---|---|
| **A** | [General .NET](#session-a--general-net) | 60 min | C#, a debugging ticket, an API problem, a code review |
| **B** | [SDK / Migration](#session-b--sdk--migration) | 75 min | API design, workflow, file transfer, recovery, architecture |
| **C** | [WPF](#session-c--wpf) | 55 min | Binding, UI thread, a leak, reuse |
| **D** | [MAUI](#session-d--maui) | 55 min | Navigation, DI, lifecycle, shared-core architecture |
| **E** | [Full mock](#session-e--full-mock) | 90 min | The whole role in one sitting |

Pick **A** first if you have not practised interviewing recently. Pick **B** if you want the session closest
to the actual job. **E** is the dress rehearsal — do it once, late.

---

## Session A – General .NET

**60 minutes.** Breadth: can this person read unfamiliar C#, find a bug, integrate with an API, and review
someone else's work?

| Time | Exercise | What it is |
|---|---|---|
| 0:00–0:05 | — | Introductions, check the build runs |
| 0:05–0:15 | [01-01 Project totals are wrong](exercises/01-csharp/01-01-project-totals-wrong/) | A counter that reports the wrong numbers |
| 0:15–0:27 | [02-01 The migration finishes too early](exercises/02-debugging/02-01-migration-finishes-too-early/) | A support ticket: the migration reports success while files are still going up |
| 0:27–0:40 | [05-02 The server upgrade that broke every desktop](exercises/05-http-api/05-02-server-upgrade-breaks-client/) | The platform changed; 200 laptops stopped working |
| 0:40–0:58 | [15-01 Review this PR: bulk upload](exercises/15-code-review/15-01-review-bulk-upload/) | Review a colleague's pull request |
| 0:58–1:00 | — | Questions, close |

**Looking for:** reading code you did not write; forming a hypothesis before changing anything; knowing what
a client owes a server that changes underneath it; and whether your review comments would be welcome.

---

## Session B – SDK / Migration

**75 minutes.** The session closest to the job. Design, then the hard parts of doing it for real.

| Time | Exercise | What it is |
|---|---|---|
| 0:00–0:20 | [09-01 Design the SDK's public API](exercises/09-sdk-design/09-01-design-the-public-api/) | Whiteboard the public surface from a requirements page |
| 0:20–0:35 | [10-01 A migration that says "Completed"](exercises/10-migration/10-01-migration-workflow-states/) | A state machine that reports the wrong thing |
| 0:35–0:55 | [07-01 The 100 GB file](exercises/07-file-transfer/07-01-large-file-upload/) | Upload a file far larger than memory |
| 0:55–1:05 | [10-04 The upload that might have worked](exercises/10-migration/10-04-lost-upload-response/) | The request left, the response never came back |
| 1:05–1:15 | [16-02 From 100 MB to 100 GB](exercises/16-system-design/16-02-from-100mb-to-100gb/) | What breaks when the customer is 50× bigger |

**Looking for:** API design that survives a second consumer; treating failure as a normal outcome; streaming
rather than buffering; and knowing that an ambiguous result is a design problem, not an error-handling one.

---

## Session C – WPF

**55 minutes.** Desktop work as this role actually encounters it.

| Time | Exercise | What it is |
|---|---|---|
| 0:00–0:12 | [11-01 The status panel that never updates](exercises/11-wpf/11-01-status-panel-not-updating/) | Bindings that silently do nothing |
| 0:12–0:27 | [11-02 Progress from a background migration](exercises/11-wpf/11-02-background-migration-progress/) | Migration on a worker thread, progress on the UI |
| 0:27–0:39 | [11-03 The app that grows](exercises/11-wpf/11-03-memory-grows-per-migration/) | Memory climbs with every migration |
| 0:39–0:55 | [11-04 A control only one app can use](exercises/11-wpf/11-04-reusable-file-list-control/) | Make a control reusable |

**Alternative for the last slot** if you want a discussion rather than code:
[15-02 Review this PR: the migration window](exercises/15-code-review/15-02-review-migration-window/).

**Looking for:** knowing why a binding fails silently; the dispatcher and how not to flood it; what keeps an
object alive; and the difference between a control that works and one another app can use.

---

## Session D – MAUI

**55 minutes.** For a developer moving from WPF to MAUI — which is what this role asks for.

| Time | Exercise | What it is |
|---|---|---|
| 0:00–0:15 | [12-02 The board that doesn't update](exercises/12-maui/12-02-migration-list-not-updating/) | Four symptoms, more than one cause |
| 0:15–0:35 | [12-01 The detail flow a WPF developer wrote](exercises/12-maui/12-01-detail-flow-navigation/) | Navigation and DI done the WPF way |
| 0:35–0:45 | [12-03 Killed in the background](exercises/12-maui/12-03-app-killed-in-background/) | The OS kills the app mid-migration |
| 0:45–0:55 | [16-01 Design the migration SDK](exercises/16-system-design/16-01-design-the-migration-sdk/) | One core, two very different apps (discuss the WPF/MAUI constraint only) |

**Looking for:** Shell navigation and parameters rather than global state; DI instead of a service locator;
the main-thread rule; and knowing which assumptions from desktop do not survive on a phone.

> Requires the `maui-windows` workload. If it is not installed, run session C and use 12-03 and 16-01 as
> discussions — they need no build.

---

## Session E – Full mock

**90 minutes.** The dress rehearsal: every part of the role, at interview pace.

| Time | Exercise | What it is |
|---|---|---|
| 0:00–0:05 | — | Introductions, check the build runs |
| 0:05–0:15 | [02-02 The failed migration that reported success](exercises/02-debugging/02-02-failed-migration-reports-success/) | Debugging warm-up |
| 0:15–0:25 | [01-03 "Provisioning failed. Please contact support."](exercises/01-csharp/01-03-provisioning-results/) | Modelling failure in C# |
| 0:25–0:37 | [03-01 Five hundred files at once](exercises/03-async/03-01-five-hundred-files/) | Async and bounded concurrency |
| 0:37–0:47 | [04-02 "Start migration" clicked twice](exercises/04-concurrency/04-02-start-clicked-twice/) | A race a user can trigger |
| 0:47–1:02 | [08-01 The platform has bad minutes](exercises/08-reliability/08-01-transient-server-errors/) | Retries that help rather than hurt |
| 1:02–1:15 | [11-02 Progress from a background migration](exercises/11-wpf/11-02-background-migration-progress/) *or* [12-02 The board that doesn't update](exercises/12-maui/12-02-migration-list-not-updating/) | UI work, on whichever framework you know better |
| 1:15–1:30 | [16-03 Migrating on a bad network](exercises/16-system-design/16-03-migrating-on-a-bad-network/) | Design discussion to close |

**Looking for:** consistency. The same person should debug methodically at 0:05 and still reason clearly at
1:20. Fatigue is part of what this session measures.

---

## Variants

To stop a second run being a memory test, each session can be varied without changing what it measures:

- **Swap within a category.** 02-01 ↔ 02-05, 03-01 ↔ 03-03, 07-01 ↔ 07-04, 11-01 ↔ 11-03, 12-02 ↔ 12-04.
- **Change the data.** Several exercises read `shared/MockData`; point them at `datasets/large` or
  `datasets/malformed` for a different failure.
- **Change the failures.** Exercises that use the mock server can be given a different fault sequence through
  `/control/fault` — see [shared/MockServer/API.md](shared/MockServer/API.md). "Now it returns 429 instead of
  500" is a different conversation with the same learning objective.
- **Change the numbers.** Concurrency limits, file sizes and chunk sizes are constants in the exercises; a
  different value changes the arithmetic without changing the problem.

## Self-practice

Running a session alone works if you are strict about two things: keep the clock, and write your reasoning
down before you touch the code. Afterwards, read the `SOLUTION.md` for each exercise and mark yourself against
the scoring notes in [`interviewer/rubrics/`](interviewer/rubrics/) — the list of what you did not think of is
the actual output of the session.
