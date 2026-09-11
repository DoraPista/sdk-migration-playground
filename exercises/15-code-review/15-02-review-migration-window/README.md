# Exercise 15-02 – Review This PR: The Migration Window

Difficulty: Medium
Estimated Time: 25 minutes

## Skills

- code review of desktop (WPF) code
- UI threading, lifetime and testability
- communicating findings

## Scenario

The same colleague has opened a second pull request: the window that runs a migration and shows progress.
QA have already signed it off ("works, progress bar moves"). Support have not seen it yet.

Read `PR.md`, then `src/MigrationWindow.xaml`, `src/MigrationWindow.xaml.cs` and `src/MigrationRunner.cs`.

## Your Task

Review it.

- Which findings would you block the merge on?
- Which are about *this* code, and which are about where the code lives (view, view model, service)?
- What would you want to see tested, and what would that require changing?

You are not asked to rewrite the window.

## Constraints

- Time-box the reading to ten minutes.
- Assume the team uses MVVM elsewhere in the product, and this window does not.

## How to Run

Nothing to run. The project compiles:

```bash
dotnet build exercises/15-code-review/15-02-review-migration-window/src
```

Write your findings in `REVIEW.md` (copy `REVIEW_TEMPLATE.md`) or say them out loud.

## What to Produce

A prioritised list of findings with the effect on a user, what you would want tested, and what moving the
code would (and would not) fix.

## When You're Done

You have a prioritised list, and you can say what you would move where — and why that is worth doing now
rather than "later".
