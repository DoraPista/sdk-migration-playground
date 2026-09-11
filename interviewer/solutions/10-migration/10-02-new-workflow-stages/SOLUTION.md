# 10-02 Two More Stages – Interviewer Notes

**Type:** refactoring + feature work · **Time:** 30 min · **Solution code:** `code/src/`

## Hints

1. "Before adding anything: what shape would let a new stage be one class and one line?"
2. "The existing tests are the contract. Restructure with them green first, then add the two stages."
3. "An IMigrationStage with ShouldRun and ExecuteAsync, a context object carrying state between stages, and an ordered list the workflow walks."

Two things are being assessed: **restructuring safely** (the existing tests are the contract) and **writing the new rules**.
Watch the order they work in. Restructure first, then add stages, running the tests throughout, is the answer you want to see.

## Intended solution

- `IMigrationStage` with `Name`, `ShouldRun(context)` and `ExecuteAsync(context, ct)`.
- `MigrationContext` carries state between stages (destination, migration id, counters, issues).
- `MigrationWorkflow.RunAsync` becomes a loop: skip stages that shouldn't run, record the ones that do, stop at the first failing stage.
- The workflow **is** the ordered list in `BuildStages`, so adding a stage is one class plus one line.
- New stages: `ValidateStage` (before `CreateMigration`) and `ThumbnailStage` (after the files).
- Validation rules in one place, one method per rule.

### Alternatives

- A **pipeline of middleware** (`Func<Context, Func<Task>, Task>`, like ASP.NET Core). More flexible (a stage can wrap the rest: timing, retries), more machinery. A good senior answer, especially with the follow-up "how would you time every stage?"
- Keep one method but extract each stage into a private method and call them in order. Better than today; doesn't stop the next `if` from appearing; the option handling is still spread out.
- Stage list built by DI (`IEnumerable<IMigrationStage>` in registration order). Neat; ordering becomes implicit, which some teams regret.

## What to look for

| Aspect | Good sign |
|---|---|
| Options | `ShouldRun` replaces `if (options.X)` around whole stages; `DryRun` handled inside the stages that have side effects (it must still *list* the stage, which the existing tests pin) |
| Ordering | Explicit and visible in one place |
| Failure | A stage returns a failure; the driver stops. No `throw` for expected outcomes |
| Cancellation | Checked between stages and inside the file loop |
| Validation | Runs before `CreateMigration`; reports **every** issue, not just the first |
| Rules | Each rule separate; the unsafe-path rule catches `..`, rooted paths and `C:` (the malformed dataset has one of each) |

## Common mistakes

- Restructuring and changing behaviour at the same time, then debugging two things at once.
- `ShouldRun` returning false for `DryRun`, which removes the stage from `StagesRun` and breaks the existing test.
- Validation that stops at the first issue (the customer wants the whole list in one go).
- An unsafe-path rule that only checks `StartsWith("..")` (misses `documents/../../x`) or only `Contains("..")` (flags a legitimate `..dotfile`; the solution splits into segments).
- Making `IMigrationStage` public with a `virtual` base class "for extensibility" without being asked (this is an internal design; see 09-01 for what's public).
- Forgetting that `Verify`/`Complete` must not run when validation failed.

## Follow-up questions

- "A stage needs to run only when the customer is on the Enterprise tier. Where does that go?" (`ShouldRun`, with the tier in the context.)
- "How would you time every stage and log it?" (Decorator around `IMigrationStage`, or middleware. This is where the pipeline design pays off.)
- "The thumbnail stage is slow for 50,000 images. What would you change?" (Parallelism with a limit, 03-01; or do it on the platform.)
- "Where would resume fit into this?" (Stages become resumable units; see 10-03.)

## Scoring notes

| Level | Indicators |
|---|---|
| Weak | Adds two more `if` blocks to `RunAsync`; validation returns the first problem; breaks an existing test and doesn't notice |
| Solid mid-level | Clean stage abstraction, existing tests green, both features work, rules complete |
| Strong | Keeps behaviour identical while restructuring (runs tests between steps), handles DryRun correctly, writes readable rules with good messages |
| Senior | Discusses middleware vs stage list, cross-cutting concerns (timing, retries, checkpoints), and how to keep the ordering honest as the team grows |
