# Exercise 14-01 – Planning Takes Eleven Minutes

Difficulty: Medium
Estimated Time: 25 minutes

## Skills

- finding the real bottleneck
- N+1 calls, quadratic scans, needless work
- measuring instead of guessing

## Scenario

Before a migration starts, the SDK builds a **plan**: for every file, where it will go on the platform
(the project path) and how big it is. For a small customer this takes a second. For Northwind's export
(≈ 5,000 files across 150 projects) the app is unresponsive for **eleven minutes**, and the customer's
IT lead thinks it has hung.

The project catalog is a service call (it takes a few milliseconds each time, and the platform rate-limits it).

## Your Task

1. Where is the time going? Name the causes before you change anything.
2. How would you measure it in a real app, rather than by reading code?
3. Make `MigrationPlanner.BuildPlanAsync` fast, keeping the plan identical.
4. What does your change cost (memory, complexity, correctness risks)?

## Constraints

- Keep the public API and the contents of the plan.
- `IProjectCatalog` is the only way to get project information. Look at everything it offers.

## Acceptance Criteria

- The catalog is not asked about the same project over and over.
- Planning a large export does not allocate hundreds of megabytes.
- The plan (items, order, total bytes, duplicate count) is unchanged.

## How to Run

```bash
dotnet test exercises/14-performance/14-01-slow-migration-planning/tests
```

The large test uses `shared/MockData/datasets/large/`. Be patient with the first (unfixed) run.

## When You're Done

All tests pass, and you can answer the four questions above.
