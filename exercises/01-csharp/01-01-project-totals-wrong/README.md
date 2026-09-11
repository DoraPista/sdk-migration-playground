# Exercise 01-01 – Project Totals Are Wrong

Difficulty: Easy
Estimated Time: 15 minutes

## Skills

- reading unfamiliar code
- LINQ / collections
- equality and keys
- working with messy real-world data

## Scenario

When a migration finishes, the desktop app shows a per-project report built from the per-file
results the migration agent writes to disk (`shared/MockData/datasets/results/upload-results.json`
is a real example from Northwind Architects).

Northwind's IT lead sent this to support:

> "The report lists more projects than we have. Two of them look like the same project.
> One of the drawings was uploaded (we can see it in the portal), but the report
> still says it failed, and the failed count doesn't match what we see."

The results file comes from the legacy migration agent. It is not going to change.

## Your Task

Find out why the report is wrong and fix `MigrationReportBuilder`.

## Constraints

- Keep the public shape of `MigrationReport` / `ProjectSummary`.
- The report must use the canonical project ID format (`PRJ-2002`), which is what the
  portal shows.
- The agent can write several results for one file, for example when it retries the file.
  The most recent result for a file is the one that counts.

## Acceptance Criteria

- Each project appears once in the report.
- A file counts once: as uploaded or as failed, never both.
- The totals agree with the per-project numbers.

## How to Run

```bash
dotnet test exercises/01-csharp/01-01-project-totals-wrong/tests
```

## When You're Done

All tests pass, and you can explain what was wrong with the data and the code.
