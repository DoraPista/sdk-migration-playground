# Exercise 10-02 – Two More Stages

Difficulty: Medium
Estimated Time: 30 minutes

## Skills

- extending code without making it worse
- pipeline / composition design
- writing validation rules against real data

## Scenario

`MigrationWorkflow.RunAsync` has grown for two years. Every feature added another flag to
`MigrationOptions` and another `if` to the method. Product wants two more things:

**1. Pre-flight validation.** Customer exports arrive with problems. Support has a list of the ones that
must stop a migration (`shared/MockData/datasets/malformed/` is a real example of such an export):

| Code (`ValidationCodes`) | Rule |
|---|---|
| `duplicate-file-id` | Two entries share a file ID |
| `unknown-project` | An entry's project ID is not in the job's project list |
| `negative-size` | `SizeBytes` is negative |
| `invalid-hash` | `Sha256` is present but not 64 hex characters |
| `unsafe-path` | The relative path is absolute or escapes the export root (`..`) |
| `empty-path` | The relative path is empty or whitespace |

**2. Thumbnails.** When `GenerateThumbnails` is on, generate a thumbnail for every image (`Kind == "Image"`)
using the `IThumbnailGenerator` the host supplies, and report how many were generated.

Your tech lead's review comment on the last PR:

> "The next person who adds an `if` to `RunAsync` owes the team cake. Please restructure it first."

## Your Task

Restructure the workflow so stages can be added without growing one method, then add the two stages.

## Constraints

- Keep the public API (`MigrationWorkflow.RunAsync`, `WorkflowReport`, `MigrationOptions`, `ValidationIssue`).
- Keep the existing stage order and the existing option behaviour: the current tests must keep passing.
- Validation runs before anything is created on the platform, and blocking issues stop the migration.

## Acceptance Criteria

- Validation reports every problem in the export, using the codes above.
- Thumbnails are generated only for images, and only when enabled.
- Existing behaviour is unchanged.

## How to Run

```bash
dotnet test exercises/10-migration/10-02-new-workflow-stages/tests
```

## When You're Done

All tests pass, and you can explain how someone adds a third stage next month, and what stops them from getting it wrong.
