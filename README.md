# .NET Interview Gym

Practice exercises for a mid-level .NET developer preparing for a role building a **migration agent SDK**: a
reusable library, used by desktop apps (WPF and MAUI), that moves files and their metadata into a cloud platform
over networks that are not reliable. Every exercise is a problem that could have arrived as a support ticket, a
pull request or a design meeting. None of them are puzzles.

## Start here

1. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download).
2. In the repository folder, type:

   ```powershell
   .\gym
   ```

   A menu opens. **Continue the plan** takes you to the next exercise for the Brillio and Sage interviews.
   **Browse all exercises** lets you pick any of the 56. For each exercise you can read the brief, open it in your
   editor, run or watch its tests, and reset it.
3. Most exercises **start red on purpose**: the failing tests are the bug report. Change the code until they pass,
   and the gym marks the exercise as done.

The first time, `.\gym` takes a little while to start while .NET builds it; after that it is quick.

## The gym command

Everything in the menu is also a command:

| Command | What it does |
|---|---|
| `.\gym next` | Open the next exercise in the interview plan |
| `.\gym progress` | See what you have done, stage by stage |
| `.\gym list [filter]` | List exercises: `list 07`, `list fix`, `list todo`, `list token` |
| `.\gym open <id>` | Open an exercise in VS Code, Cursor, Visual Studio or Rider |
| `.\gym test <id>` | Run its tests and show which pass and which fail, in plain sentences |
| `.\gym watch <id>` | Run its tests again every time you save a file |
| `.\gym show <id>` | Print an exercise's brief |
| `.\gym run <id>` | Run its demo program, or the MAUI app for 12-01 to 12-03 |
| `.\gym done <id>` | Mark a discussion or review exercise as done (`todo <id>` undoes it) |
| `.\gym reset <id>` | Undo your changes to an exercise, after asking |
| `.\gym server` | Start the mock platform on http://localhost:5080 |

`<id>` is a number such as `07-02` (or `7-2`), or a word from the exercise's name: `.\gym test flaky`. All
exercises are also listed in [EXERCISES.md](EXERCISES.md), and every exercise's README gives the plain
`dotnet test` command.

## Four kinds of exercise

| Type | What you do |
|---|---|
| **Fix** | Code with failing tests. Make them pass, and be ready to explain why your change is right |
| **Discussion** | A design problem with no tests. Talk it through or sketch it, as you would in the interview |
| **Code review** | Read a pull request and write the review |
| **MAUI app** | Fix a screen in the MAUI app |

## Preparing for Brillio and Sage

| Read | For |
|---|---|
| [PREP_PLAN.md](PREP_PLAN.md) | What to prepare before each of the three interviews. `.\gym next` follows its order |
| [prep/](prep/) | Quick-fire questions, Sage interview questions, the story bank and the Sage context |
| [INTERVIEW_MODE.md](INTERVIEW_MODE.md) | Five complete mock interview sessions of 45–90 minutes |
| [JOB_COVERAGE.md](JOB_COVERAGE.md) | Which exercises cover which requirement in the job ad |

Working out loud matters more than finishing. "I would measure this before changing it" is worth more than a
lucky fix.

## What's in the repository

```text
dotnet-interview-gym/
├── gym.cmd               the gym: type .\gym for the menu
├── EXERCISES.md          every exercise in one list
├── PREP_PLAN.md          the Brillio → Sage interview plan
├── INTERVIEW_MODE.md     five mock interview sessions
├── JOB_COVERAGE.md       job requirement → exercises
├── InterviewGym.slnx     every project except the MAUI app (open this in Visual Studio or Rider)
├── exercises/            16 categories, 56 exercises
├── prep/                 quick-fire and Sage questions, story bank, Sage context
├── shared/               the mock platform, mock data and test helpers the exercises use
├── tools/                the source of the gym command
├── docs/SETUP.md         setup details: the MAUI workload, the mock platform, path length, line endings
└── interviewer/          answers, hints and rubrics: don't open while practising
```

## About the answers

`interviewer/` holds the solutions, hints and scoring rubrics. Reading them before attempting an exercise wastes
the exercise. To stop them turning up by accident, search and quick-open in VS Code and Cursor skip that folder
(`.ignore`), and Cursor's AI cannot read it (`.cursorignore`). You can still open the files directly.

Interviewers start at [interviewer/README.md](interviewer/README.md).
