# Scoring Model

One scale, used for every exercise and every dimension.

```text
5 — Excellent
4 — Strong
3 — Solid mid-level
2 — Weak
1 — Significant gaps
```

**3 is a pass for this role.** The role is mid-level; 4s and 5s are information about where the person could
grow, not a requirement. A candidate with several 3s and one 2 in a dimension the job leans on (concurrency,
reliability) is a different decision from one with several 3s and a 2 in, say, Azure familiarity.

## Dimensions

Score only the dimensions an exercise actually exercises. Most exercises touch four or five; a whole session
should cover all twelve.

| Dimension | 2 — Weak | 3 — Solid mid-level | 5 — Excellent |
|---|---|---|---|
| **Correctness** | Changes code until the test passes; cannot say why it now works | Finds the real cause and fixes it; handles the obvious edge cases | Fixes the cause, names the cases the tests do not cover, and says which still worry them |
| **Reasoning** | Jumps to a conclusion and defends it | States a hypothesis, checks it, revises it | Reasons from evidence, weighs two explanations, and says what would distinguish them |
| **Debugging methodology** | Random changes; reads code without running it | Reads the failing test, forms a hypothesis, narrows it down | Systematic: bisects, adds targeted instrumentation, and knows when to stop reading and start measuring |
| **Code quality** | Leaves the code worse; copy-paste; dead code | Readable, consistent with the surrounding style | Simplifies while fixing; the diff is smaller than expected and clearer than before |
| **API design** | Exposes internals; parameters that must be passed in the right order | Small surface, obvious call site, cancellation and progress considered | Designs for the second consumer and for the next version; knows what will be hard to change |
| **Error handling** | Swallows, or wraps everything in one `catch (Exception)` | Distinguishes fatal from per-item; preserves context; fails loudly when it must | Models failure as data where it belongs; thinks about what the user and support see |
| **Concurrency awareness** | Believes `lock` solves it, or does not see the race | Identifies the interleaving; picks a correct and simple mechanism | Reasons about the full state space, avoids shared state instead of guarding it, and tests it deterministically |
| **Testing** | "It works on my machine"; no test written or changed | Writes or repairs a test that would have caught the bug | Tests the behaviour not the implementation; makes time, threads and failure controllable |
| **Communication** | Silent, or narrates without informing | Explains what they are doing and why, at a pace the listener can follow | Adjusts to the listener, admits uncertainty precisely, and disagrees well |
| **Trade-off awareness** | One solution, presented as the only one | Names an alternative and why they chose theirs | Quantifies the trade-off, and says what would change their mind |
| **Maintainability** | Adds a flag to an already-complicated method | Leaves the code easier to change than they found it | Refactors the shape that caused the bug, with the tests as a safety net |
| **Security awareness** | Logs or stores secrets without noticing | Spots credentials in logs, unvalidated input, disabled TLS | Thinks about blast radius, what a support bundle contains, and least privilege |

## How to score

1. Score each exercise immediately after it, before the next one. Memory blends sessions together.
2. Write one sentence of evidence per score. "3 — found the missing await by reasoning about `IsEmpty`,
   needed hint 2 for the exception handling" is useful in a debrief; a bare number is not.
3. **Record which hint level you had to give.** Level 1 for an exercise outside their strengths is normal.
   Level 3 on two exercises in a row is a signal.
4. Score the *session*, not the best moment in it.

## What not to do

- **Do not score purely on whether the tests went green.** A candidate who reaches the right solution while
  explaining the reasoning must score higher than one who happened to make the tests pass. If the tests pass
  and the explanation is wrong, the score is the explanation's.
- **Do not reward speed on its own.** Finishing 07-01 in eight minutes and being unable to say why streaming
  matters is a 2 for reasoning, whatever the clock says.
- **Do not penalise asking for a hint.** Penalise not asking for forty minutes.
- **Do not penalise looking things up.** Penalise not knowing what they are looking for.
- **Do not confuse confidence with competence**, in either direction. Quiet and correct beats fluent and wrong.

## Recording sheet

```text
Candidate:                              Date:            Session:
Exercise            Dimensions scored                       Hint level   Notes
──────────────────  ──────────────────────────────────────  ──────────   ────────────────────────
01-01               correctness 3, reasoning 3, testing 2    1            key normalisation; no test added
…

Overall:  ready for the role / close, with gaps in …  / not yet
Strongest:                          Weakest:
Would I put this person in front of a customer's failing migration? yes / with support / no
```
