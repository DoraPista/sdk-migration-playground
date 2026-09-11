# Exercise 15-01 – Review This PR: Bulk Upload

Difficulty: Medium
Estimated Time: 25 minutes

## Skills

- code review
- spotting concurrency, reliability and security problems in someone else's code
- communicating findings

## Scenario

A colleague has opened a pull request that adds bulk upload to the migration SDK. It works on their machine
with a 12-file test project, and they would like to merge it today because a customer demo is on Thursday.

Read `PR.md` for their description, then the code in `src/`.

## Your Task

Review the PR as you would at work.

- What would block the merge? What would you raise but not block on? What is fine?
- Say *why* each finding matters — the effect on a customer, not the rule it breaks.
- Pick the three most important things. If you only got one comment, which would it be?

You are not asked to rewrite the code. One or two lines of suggested code per finding is plenty.

## Constraints

- Time-box the reading to ten minutes, then talk.
- The author is in the room. Phrase the comments as you would to them.

## How to Run

There is nothing to run. The project compiles:

```bash
dotnet build exercises/15-code-review/15-01-review-bulk-upload/src
```

Write your findings in `REVIEW.md` (copy `REVIEW_TEMPLATE.md`) or say them out loud.

## What to Produce

A prioritised list of findings — blocking and non-blocking — each with the effect on a customer, plus the
questions you would ask the author.

## When You're Done

You have a prioritised list, you can defend the ordering, and you have said what you would ask the author
rather than assert.
