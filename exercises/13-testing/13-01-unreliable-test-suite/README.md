# Exercise 13-01 – The Test Suite Nobody Trusts

Difficulty: Medium
Estimated Time: 25 minutes

## Skills

- what makes a test valuable
- deterministic tests
- test smells

## Scenario

`ChunkedUploader` is fine. Its test suite is not:

- The build takes 40 seconds longer than it should, and about one run in five fails "for no reason".
- One test only passes if the whole class runs in order.
- A test hits `http://localhost:5999`, which nobody has running.
- Two tests have never failed, including the week the uploader was accidentally returning `null`.

The team's rule: *"a red build means the product is broken"*. Right now nobody believes that, so failures get re-run
until they go green.

## Your Task

Fix the test suite: same (or better) coverage of the same behaviour, without the problems above.

You may add, delete, rename, split or rewrite tests; `ChunkedUploaderTests.cs` is yours. The point is
*what* you keep, what you change, and what you can justify.

## Constraints

- Don't change `src/` unless a test reveals a real defect. If it does, fix it and say so.
- The suite must run offline, in any order, in under a second.
- Keep testing the same behaviours: chunking, retry, hashing, cancellation, progress.

## Acceptance Criteria

- No sleeps, no network, no order dependencies, no tests that cannot fail.
- Every test's name says what behaviour it protects.

## How to Run

```bash
dotnet test exercises/13-testing/13-01-unreliable-test-suite/tests
```

(Expect the suite to be slow and partly red before you start. That is the exercise.)

## When You're Done

You can explain, test by test, what was wrong with it and what your version guarantees.
