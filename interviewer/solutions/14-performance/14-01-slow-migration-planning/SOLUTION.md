# 14-01 Planning Takes Eleven Minutes – Interviewer Notes

**Type:** performance · **Time:** 25 min · **Solution code:** `code/src/MigrationPlanner.cs`

## Hints

1. "Do not read for style. Count: how many network calls, and how much work, does one file cost?"
2. "Two lines inside the loop do work proportional to everything done so far."
3. "Batch the project lookups - the interface already has the method - cache each project path, use a HashSet for duplicates, and serialize the plan once after the loop."

## The four questions

### 1. Where does the time go?

| Cause | Measured, planning 1,500 files across 150 projects (the test suite prints these) |
|---|---|
| **N+1 calls**: `GetProjectAsync` per file, plus one per ancestor while walking the parent chain | **6,080 requests for 150 distinct projects.** At 3 ms each that is 18 s of pure latency, and the catalog rate-limits |
| **Serializing the whole plan on every iteration** (`JsonSerializer.Serialize(items)` inside the loop) | O(n²) work: ~1.1 million item serializations, and the bulk of the **352 MB** the test reports. Usually the biggest single cost |
| **Quadratic duplicate check** (`items.Any(i => i.RelativePath == …)`) | ~1.1 million string comparisons (and 12.5 million at the full 5,000 files) |
| Rebuilding each project path from scratch | Repeated work per file, even though 150 paths exist |

The interesting part: the *obvious* suspect (network calls) is real, but the JSON serialization inside the loop
is what makes it minutes rather than seconds. That is why question 2 matters.

### 2. How would you measure it?

Expect concrete tools: `dotnet-counters`/`dotnet-trace` on the running app, a profiler (Visual Studio, PerfView,
dotTrace) with a sampled call tree; `Stopwatch` around phases as a first cut; counting catalog calls with a
counter or logging; `GC.GetTotalAllocatedBytes` (as the test does) for allocation; BenchmarkDotNet for a micro
comparison of a fix. Also: reproduce with the *large* dataset, not the small one.

### 3. The fix

- One batched call (`GetProjectsAsync`, already on the interface and unused), plus a small loop for ancestors.
- A path cache per project id.
- `HashSet` for duplicates.
- Serialize the plan **once**, after the loop.

### 4. What does it cost?

- Memory: all projects and the plan are held at once (150 projects and 5,000 items: tiny; for 5 million files the
  plan itself becomes the problem, and streaming/batching would be the next step).
- Complexity: a little; the batching loop needs care with cycles (`parentId == id`) and missing parents (both handled).
- Correctness risk: the plan's **order and contents must not change**; that is what the first two tests pin down.

## Common mistakes

- Parallelising the N+1 calls (`Task.WhenAll` over 5,000 requests) instead of removing them: faster, and it hammers a rate-limited service (see 03-01, 08-03).
- Caching `GetProjectAsync` results in a dictionary but keeping one request per distinct project (150 requests instead of 1: acceptable, but the batch method exists).
- Removing `PlanSizeInBytes` entirely (it is a feature; compute it once).
- Using `Distinct()` on `PlanItem` for duplicates (different semantics: the count would change).
- Fixing only the serialization and declaring victory without checking the call count.

## Follow-up questions

- "The catalog has no batch endpoint. What then?" (Distinct + parallel with a small limit, plus caching; ask the platform team for one.)
- "The export has 5 million files. What breaks next?" (Memory for the plan and the JSON; stream the plan, checkpoint it, or plan per project.)
- "How would you keep this fast as the code evolves?" (The call-count and allocation tests in this exercise are exactly that: they fail if someone reintroduces an N+1.)

## Scoring notes

| Level | Indicators |
|---|---|
| Weak | Guesses; parallelises the calls; misses the in-loop serialization |
| Solid mid-level | Finds N+1, the quadratic scan and the serialization; batches and caches; keeps the plan identical |
| Strong | Explains how they would measure, notices the unused batch API, handles cycles/missing parents |
| Senior | Discusses scale limits, rate limiting, and locking in the fix with call-count/allocation tests |
