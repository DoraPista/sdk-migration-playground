# Exercise 13-02 – Tests for Retry and Cancellation

Difficulty: Medium
Estimated Time: 30 minutes

## Skills

- writing tests for time-dependent behaviour
- testing cancellation paths
- making code testable

## Scenario

`ResilientUploader` retries failed uploads with exponential backoff (1, 2, 4, 8 seconds) and, when the user
cancels, tells the platform to abort the upload session so the partial file does not sit there costing storage.

It has one test for the happy path and one that has been `[Skip]`ped for six months because it takes 15 seconds.

Support has an open ticket: *"the platform says we have 4,000 abandoned upload sessions from cancelled
migrations"*. Nobody has reproduced it.

## Your Task

Write the tests this class should have:

- the retry behaviour (how many attempts, that a retry re-sends the same content, and when it gives up),
  **without the suite taking seconds**,
- the cancellation path, including what the uploader must do on the platform when the user cancels.

If your tests find a defect, fix it and say what it was.

## Constraints

- You may change `src/` (including constructor parameters) to make it testable.
- The whole test suite must run in under a second and must not depend on wall-clock timing.
- `ResilientUploader.UploadAsync` keeps its signature.

## Acceptance Criteria

- Retry and give-up behaviour is covered by tests that don't wait for real time.
- Cancellation is covered, including the clean-up the platform expects.
- `dotnet test` is green and fast.

## How to Run

```bash
dotnet test exercises/13-testing/13-02-testing-retries-and-cancellation/tests
```

## When You're Done

You can explain how you made time testable, and what the support ticket was about.
