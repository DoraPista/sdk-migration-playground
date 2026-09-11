# 01-01 Project Totals Are Wrong – Interviewer Notes

**Type:** debugging (warm-up) · **Time:** 15 min · **Solution code:** `code/src/MigrationReportBuilder.cs`

## What is wrong

| # | Defect | Symptom |
|---|---|---|
| 1 | Grouping key is the raw `ProjectId`. The legacy agent writes `prj-2002` and `PRJ-2005 ` (lower case, trailing space) | "More projects than we have", with near-duplicate rows |
| 2 | Every result line is counted. A retried file has a failed line and a later successful line | File counted as failed *and* uploaded; failed count too high |
| 3 | "Last line wins" is not enough either. The results file is not ordered by time (F-R009's success comes before the failure in the test data) | A naive "overwrite in a dictionary" fix still gets the retry case wrong |

The test failures point straight at (1) and (2). (3) is what separates careful candidates from quick ones.

## Hints

1. *Broad:* "Compare the project IDs in the results file with what the portal shows."
2. *Specific:* "What is the key of your dictionary, and how does the file store it? And how many lines can one file have?"
3. *Strong:* "Normalise the key before grouping, and reduce the results to one per file first. Which line should win?"

## Intended solution

1. Reduce to one result per `FileId`, choosing the latest by `CompletedAt` (`GroupBy` + `MaxBy`).
2. Group by a canonical key (`Trim().ToUpperInvariant()`) and **output** the canonical ID.
3. Derive the totals from the reduced set.

### Alternative valid solutions

- A `Dictionary<string, UploadResult>` keyed by file ID that keeps the entry with the later timestamp.
- A custom `IEqualityComparer<string>` for project IDs (`StringComparer.OrdinalIgnoreCase` plus trimming) used by `GroupBy`.
- Normalising at the reading boundary (in `UploadResultsFile.Read`) instead of in the builder. This is a good point to discuss: normalise at the edge vs. in the domain.

## Common mistakes

- Using `StringComparer.OrdinalIgnoreCase` but forgetting the trailing whitespace.
- Grouping case-insensitively but outputting whichever variant came first (`prj-2002`). This breaks the canonical-ID requirement.
- `Distinct()` on `UploadResult` records. Records compare *all* members, so the failed and successful lines stay distinct.
- "Last one in the list wins" (fails the ordering test).
- `ToLower()` instead of `ToUpperInvariant()`: culture-sensitive (Turkish-I problem) and the wrong canonical form.

## Edge cases to discuss

- Two results for the same file with **identical** timestamps. Which wins? (Prefer success? Prefer failure? It is ambiguous, so ask.)
- A file that moved between projects on a retry (different `ProjectId` on two lines).
- Should a file that failed and was never retried show its error message in the report?
- Where should normalisation live, and should the agent's output be validated at all?

## Follow-up questions

- "If this file had 5 million lines, what would you change?" (Stream the JSON with `DeserializeAsyncEnumerable`, and keep only a dictionary of the latest result per file.)
- "Would you make this builder part of the public SDK surface?"
- "How would you make the equality rule impossible to forget next time?" (A `ProjectId` value object with normalisation in the constructor.)

## Scoring notes

| Level | Indicators |
|---|---|
| Weak | Fixes only the case-sensitivity, or adds `.ToLower()` everywhere; doesn't notice duplicate file results; can't explain record equality |
| Solid mid-level | Finds both defects; one result per file; canonical key; tests pass |
| Strong | Picks the latest by timestamp and explains why list order can't be trusted; mentions culture-invariant comparison; proposes a `ProjectId` value type |
| Senior | Questions the data contract with the agent (what else can be dirty?); suggests validating and quarantining at the boundary; considers streaming for large files |

## Variants

- Change the duplicate so that the **failure** is the later event (report must show failed).
- Add a result with project `" PRJ-2001"` (leading whitespace) or `PRJ‐2001` (Unicode hyphen U+2010). The second is a great discussion point: how far should normalisation go?
