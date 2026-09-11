# .NET Interview Gym

A practice repository for a mid-level .NET developer preparing for a role building a **migration agent SDK**:
a reusable library, used by a WPF desktop app and a MAUI app, that moves large numbers of files and their
metadata into a cloud platform over networks that are not reliable.

Every exercise is a problem that could have arrived as a support ticket, a pull request or a design meeting.
None of them are puzzles: there is no dynamic programming here, and no "reverse a linked list".

- **56 exercises** across 16 categories, 41 of them with a compiling project and automated tests.
- A **local mock platform** (HTTP server with fault injection) and **realistic mock data**, so nothing needs
  a cloud account or a network connection.
- Interviewer-only material (solutions, hints, rubrics, mock interviews) kept entirely under `interviewer/`.

## Requirements

- **.NET 10 SDK** (`global.json` pins the major version; `dotnet --version` should report 10.x)
- Windows for the WPF (`11-*`, `14-02`, `15-02`) and MAUI (`12-*`) exercises; everything else runs anywhere
- For the MAUI exercises: `dotnet workload install maui-windows`
- Optional: the `net48` sample in `09-04` needs .NET Framework 4.8 targeting packs (Windows only)

## Getting started

```bash
git clone <this repo>
cd dotnet-interview-gym
dotnet build InterviewGym.slnx           # everything except the MAUI app
dotnet test exercises/01-csharp/01-01-project-totals-wrong/tests
```

Then open [exercises/01-csharp/01-01-project-totals-wrong/README.md](exercises/01-csharp/01-01-project-totals-wrong/README.md)
and work through it. Each exercise is self-contained: read its `README.md`, change the code in `src/`, and
run the tests in `tests/`.

```bash
dotnet test exercises/<category>/<exercise>/tests
```

Most exercises **start red on purpose**. A failing test is the bug report.

## How to use this repository

Three ways, depending on what you need:

| Mode | What you do |
|---|---|
| **Brillio / Sage interviews** | Start with [PREP_PLAN.md](PREP_PLAN.md): what to prepare before each of the three interviews |
| **Practice** | Pick a category and work through it. Time yourself against the estimate in each README |
| **Mock interview** | Follow [INTERVIEW_MODE.md](INTERVIEW_MODE.md): five complete sessions of 45–90 minutes |
| **Targeted revision** | Use [JOB_COVERAGE.md](JOB_COVERAGE.md) to find the exercises for one job requirement |

Working out loud matters more than finishing. The interviewer notes reward "I would measure this before
changing it" far more than a lucky fix.

## Layout

```text
dotnet-interview-gym/
├── README.md                 you are here
├── INTERVIEW_MODE.md         five mock interview sessions
├── JOB_COVERAGE.md           job requirement → exercises
├── PREP_PLAN.md              the Brillio → Sage interview plan
├── InterviewGym.slnx         all projects except the MAUI app
├── exercises/                16 categories, 56 exercises
├── prep/                     quick-fire questions, story bank, Sage context
├── shared/
│   ├── MockServer/           the fake platform (ASP.NET Core) + API.md
│   ├── MockData/             customers, projects, files, migrations + sample files
│   ├── Models/               the shapes of the mock data
│   └── TestUtilities/        deterministic test helpers (gates, fake clock, scripted HTTP, STA)
└── interviewer/              solutions, hints, rubrics, mock interviews — do not read while practising
```

## The 16 categories

| # | Category | Exercises | What it is about |
|---|---|---|---|
| 01 | [C# / .NET](exercises/01-csharp/) | 3 | Equality and keys, abstraction, modelling failure as data |
| 02 | [Debugging](exercises/02-debugging/) | 6 | Six tickets: fire-and-forget, swallowed errors, leaks, deadlock, stale state, wrong headers |
| 03 | [Async / await](exercises/03-async/) | 3 | Bounded concurrency, cancellation that actually cancels, partial failure |
| 04 | [Concurrency](exercises/04-concurrency/) | 3 | Races across `await`, double-start, a pipeline with backpressure |
| 05 | [HTTP / API](exercises/05-http-api/) | 2 | Building a client; surviving a server upgrade |
| 06 | [Authentication](exercises/06-authentication/) | 3 | Expiry mid-migration, secrets in logs, a token cache |
| 07 | [File transfer](exercises/07-file-transfer/) | 4 | 100 GB files, resume, idempotency, real-world data |
| 08 | [Reliability](exercises/08-reliability/) | 3 | Retry classification, long-running operations, retry storms |
| 09 | [SDK design](exercises/09-sdk-design/) | 5 | Public API, a bad API to review, UI leakage, .NET Standard 2.0, versioning |
| 10 | [Migration workflow](exercises/10-migration/) | 4 | State machines, new stages, resume after a crash, the lost response |
| 11 | [WPF](exercises/11-wpf/) | 4 | Binding, the dispatcher, leaks, a reusable control |
| 12 | [MAUI](exercises/12-maui/) | 4 | Shell navigation, collection updates, lifecycle, testable view models |
| 13 | [Testing](exercises/13-testing/) | 2 | Repairing a flaky suite; testing retries and cancellation |
| 14 | [Performance](exercises/14-performance/) | 2 | An N+1 planner; a WPF gallery that eats memory |
| 15 | [Code review](exercises/15-code-review/) | 2 | Two realistic pull requests to review |
| 16 | [System design](exercises/16-system-design/) | 6 | SDK architecture, scale, bad networks, concurrency, versioning, Azure |

## The mock platform

Several exercises talk to a local HTTP server that behaves like the real platform — OAuth2, chunked uploads,
long-running operations, rate limits — and can be told to fail in specific ways.

```bash
dotnet run --project shared/MockServer/Gym.MockServer     # http://localhost:5080
```

Its contract is documented in [shared/MockServer/API.md](shared/MockServer/API.md), including the
`/control/fault` endpoint the exercises use to inject failures. Tests that need it start it themselves
(`MockServerFixture`), so you rarely have to.

Mock data lives in `shared/MockData` — a normal dataset, a `large` one (5,000 files, 150 projects), a
`malformed` one, and real sample files including a zero-byte file, Unicode names, one corrupted file and one
that is referenced but missing. It is regenerated with:

```bash
dotnet run shared/MockData/tools/GenerateMockData.cs -- shared/MockData
```

## Moving it, copying it, or putting it on Git

The repository is self-contained and uses no absolute paths, so it works from any folder or drive. Two
things are worth knowing:

- **Keep the path reasonably short on Windows.** Build output reaches ~160 characters below the repository
  root, and Windows still enforces a 260-character limit unless long paths are enabled. A root like
  `C:\dev\dotnet-interview-gym` is fine; a deeply nested OneDrive folder may not be. To lift the limit:
  `reg add HKLM\SYSTEM\CurrentControlSet\Control\FileSystem /v LongPathsEnabled /t REG_DWORD /d 1 /f` (admin, needs a reboot).
- **Keep `.gitattributes`.** `shared/MockData` holds real file content whose SHA-256 hashes and byte sizes
  are recorded in `files.json` and asserted by the tests. With Git's default `core.autocrlf=true` on Windows,
  a commit-and-clone would rewrite those files and break several exercises for reasons that have nothing to
  do with the exercise. `.gitattributes` prevents that; it only works if it is committed *before* the data.

```bash
git init
git add .
git commit -m "dotnet interview gym"
```

If you downloaded a ZIP, Windows may mark the contents as blocked. Clear it before building:

```powershell
Get-ChildItem -Recurse | Unblock-File
```

## Resetting an exercise

Nothing outside the exercise folder is modified, so `git checkout -- exercises/<category>/<exercise>` puts it
back. If you are not using Git, each exercise's `src/` is small enough to re-read in a minute — the tests are
the specification.

## A note on the interviewer folder

`interviewer/` contains solutions, three-level hints, scoring rubrics and full session scripts. Reading it
before attempting an exercise wastes the exercise. To hand a copy of this repository to someone else,
uncomment the `/interviewer/` line in `.gitignore`, or run
`interviewer/tools/make-candidate-copy.ps1 -Destination <folder>`, which copies the repository without it.

For interviewers: start at [interviewer/README.md](interviewer/README.md).
