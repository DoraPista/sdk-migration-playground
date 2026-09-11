# Mock Interview Sessions

Five runnable sessions. Each script has the exact exercise order, timings, the prompt to read out, the
three hint levels, follow-up questions, what to watch for, and a scoring sheet.

The candidate-facing version of these (order and timings only, no hints or answers) is
[`INTERVIEW_MODE.md`](../../INTERVIEW_MODE.md) at the root.

| Session | Length | Focus | Exercises |
|---|---|---|---|
| [A – General .NET](session-a-general-dotnet.md) | 60 min | Breadth | 01-01, 02-01, 05-02, 15-01 |
| [B – SDK / Migration](session-b-sdk-migration.md) | 75 min | The actual job | 09-01, 10-01, 07-01, 10-04, 16-02 |
| [C – WPF](session-c-wpf.md) | 55 min | Desktop | 11-01, 11-02, 11-03, 11-04 |
| [D – MAUI](session-d-maui.md) | 55 min | WPF → MAUI | 12-02, 12-01, 12-03, 16-01 |
| [E – Full mock](session-e-full-mock.md) | 90 min | Everything | 02-02, 01-03, 03-01, 04-02, 08-01, 11-02/12-02, 16-03 |

## Which to run

- **First interview with an unknown candidate:** A. It is the broadest and the least dependent on one
  framework.
- **Deciding between two candidates:** B. It is closest to the work and has the clearest senior signals.
- **The candidate claims deep WPF or MAUI experience:** C or D.
- **Final round, or a self-assessment dress rehearsal:** E.

## Before any session

1. Build on the interview machine the day before: `dotnet build InterviewGym.slnx` (and `MauiGym` for D).
2. `git checkout -- exercises/` to reset.
3. Read the session script and each exercise's `SOLUTION.md`.
4. Open [`../rubrics/scoring-model.md`](../rubrics/scoring-model.md) and score as you go, not afterwards.

## Reusing a session

Every script ends with substitutions, and [`INTERVIEW_MODE.md`](../../INTERVIEW_MODE.md#variants) lists four
ways to vary a session without changing what it measures — different exercise within the category, a
different mock dataset, a different fault sequence from the mock server, or different numbers.
