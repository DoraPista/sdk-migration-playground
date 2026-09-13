# Interviewer Material

Everything in this folder is **spoilers**. Nothing here should be shown to a candidate before or during an
exercise, and none of it is referenced from candidate-facing files except as "the interviewer's copy".

To hand the repository to a candidate, uncomment the `/interviewer/` line in `.gitignore`, or delete this
folder from their copy.

## What is here

| Path | What it is |
|---|---|
| `solutions/<category>/<exercise>/SOLUTION.md` | Intended solution, alternatives, three-level hints, common mistakes, follow-ups, scoring notes |
| `solutions/<category>/<exercise>/code/` | The solution as files that overlay the exercise folder (same relative paths) |
| `solutions/16-system-design/*/DISCUSSION.md` | Discussion points for the design exercises — deliberately not "the answer" |
| `rubrics/` | The scoring model, level indicators, and the questions worth asking in any exercise |
| `mock-interviews/` | Five complete session scripts: order, timings, prompts, hints, scoring sheet |
| `tools/verify-solutions.ps1` | Runs every exercise's tests, in candidate state or with solutions applied |
| `prep/quick-fire-answers.md` | Model answers for `prep/quick-fire-questions.md` (read after answering aloud) |
| `prep/sage-interview-notes.md` | What each question in `prep/sage-interview-questions.md` probes, and what a strong answer covers |

## Before running a session

1. Read the session script in `mock-interviews/`, then the `SOLUTION.md` for each exercise in it.
2. Build once, the day before: `dotnet build InterviewGym.slnx`.
3. Reset the exercises: `git checkout -- exercises/`.
4. Have the scoring sheet from `rubrics/scoring-model.md` open.

## How the solutions are applied

Each `code/` folder mirrors the exercise's own layout, so the solution is applied by copying it over the
exercise:

```powershell
Copy-Item -Recurse -Force interviewer/solutions/07-file-transfer/07-01-large-file-upload/code/* `
                          exercises/07-file-transfer/07-01-large-file-upload/
```

`tools/verify-solutions.ps1` does this in a scratch copy of the repository, never in place:

```powershell
# every exercise, with solutions applied — all should be green
./interviewer/tools/verify-solutions.ps1

# every exercise as the candidate receives it — most should be red, on purpose
./interviewer/tools/verify-solutions.ps1 -Baseline

# one category or one exercise
./interviewer/tools/verify-solutions.ps1 -Filter '07-*'
```

Design and code-review exercises (`15-*`, `16-*`, and a few marked *Expert Discussion*) have no tests; the
script skips them.

## Using the material well

- **Do not read the solution aloud.** The hints are three levels for a reason; give the smallest one that
  unsticks the candidate, and note which level you had to reach.
- **Score reasoning, not green tests.** `rubrics/scoring-model.md` is explicit about this. Someone who
  explains the race precisely and runs out of time before fixing it is stronger than someone who
  rearranges code until the test passes.
- **The "common mistakes" sections are predictions, not accusations.** They exist so you recognise a wrong
  turn quickly and can decide whether to let it run — often the most informative thing to do.
- **Ask the follow-ups even when the exercise is solved.** They are where the mid-level/senior line shows.

## Keeping it accurate

If you change an exercise, re-run `tools/verify-solutions.ps1` in both modes. The invariant this repository
depends on is: **candidate state fails for the intended reason, solution state passes.** Every coded exercise
here has been verified in both directions.
