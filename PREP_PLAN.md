# Prep Plan — Brillio, then Sage

Three interviews, in this order:

1. **Brillio**: the employer. It is a consultancy, and you would be placed with its client, Sage.
2. **Sage, round 1**
3. **Sage, round 2**

Each one checks something different, so prepare for each separately. Everything below points at material
already in this repository.

## Before interview 1: ask the recruiter

The answers decide what is worth practising. It is normal to ask.

- What does each round cover, who runs it (engineer, manager, architect), and how long is it?
- Is there live coding? In what tool: your own IDE, a shared browser editor, an online assessment?
- Does Brillio's round include an online test, SQL, or general programming questions?
- Which Sage product and team is this for?
- What are the working hours, and how much overlap is expected with the Sage team's time zone?

---

## Interview 1 — Brillio

**What it checks:** breadth of fundamentals, your CV, and whether you are someone Brillio can confidently put
in front of a client. Expect quick-fire technical questions, questions about your own projects, possibly some
easy coding or SQL, and "why consulting?". It is less about the Sage project itself.

| Prepare | Where | Time |
|---|---|---|
| Quick-fire questions, answered **out loud**, sections A–F and J | [prep/quick-fire-questions.md](prep/quick-fire-questions.md) | 3–4 sessions of 30 min |
| Your two-minute intro and three stories | [prep/story-bank.md](prep/story-bank.md) | 1–2 hours |
| Warm-up exercises, to get used to thinking aloud | [01-01](exercises/01-csharp/01-01-project-totals-wrong/), [02-01](exercises/02-debugging/02-01-migration-finishes-too-early/), [02-02](exercises/02-debugging/02-02-failed-migration-reports-success/), [03-02](exercises/03-async/03-02-cancel-does-not-cancel/) | about 1.5 hours |
| Small functions written without IntelliSense | A plain editor: group and total a list, find duplicates, parse a line of CSV, validate brackets | 30 min |

**Have an answer ready for:** why consulting and why Brillio; how you handle a client who disagrees with you;
how you keep a remote team informed about progress and blockers.

**Questions to ask Brillio:** see [prep/sage-context.md](prep/sage-context.md#questions-to-ask-brillio).

---

## Interviews 2 and 3 — Sage

Usually one of these is a technical deep-dive with the team (coding, debugging, design), and the other is with a
hiring manager or architect (design, ownership, how you work). Ask the recruiter which is which. If you do not
know, prepare both parts for both rounds.

Read [prep/sage-context.md](prep/sage-context.md) first. It covers what is publicly known about Sage's
desktop-to-cloud migration, what the SDK probably does, and which exercises match which part of it.

Then answer the questions in [prep/sage-interview-questions.md](prep/sage-interview-questions.md) out loud: 55
questions a Sage interviewer could ask about this project, plus the coding tasks most likely to come up. The
notes on each are in `interviewer/prep/sage-interview-notes.md`.

### Technical round: the job itself

In order of how close each exercise is to the probable project:

| # | Exercise | Why it matters at Sage |
|---|---|---|
| 1 | [09-04 The customer's .NET Framework 4.8 application](exercises/09-sdk-design/09-04-legacy-host-compatibility/) | Sage's desktop products run on .NET Framework 4.7.2. The SDK is loaded into apps like that |
| 2 | [07-01 The 100 GB file](exercises/07-file-transfer/07-01-large-file-upload/) | A company backup is one large file |
| 3 | [07-02 Large files on a flaky connection](exercises/07-file-transfer/07-02-flaky-connection-large-files/) | Customer offices, customer Wi-Fi, laptops that sleep |
| 4 | [10-04 The upload that might have worked](exercises/10-migration/10-04-lost-upload-response/) | Restoring the same backup twice could create a duplicate company |
| 5 | [10-03 The app died at 63 %](exercises/10-migration/10-03-resume-after-crash/) | The desktop app is closed in the middle of a migration |
| 6 | [02-04 The desktop app hangs on connect](exercises/02-debugging/02-04-desktop-app-hangs-on-connect/) | The classic deadlock when SDK code runs inside a desktop host |
| 7 | [08-02 Provisioning takes minutes](exercises/08-reliability/08-02-provisioning-takes-minutes/) | Setting up the customer's cloud environment |
| 8 | [06-01 Token expires mid-migration](exercises/06-authentication/06-01-token-expires-mid-migration/) | Long migrations outlive their access tokens |
| 9 | [10-01 A migration that says "Completed"](exercises/10-migration/10-01-migration-workflow-states/) | Backup → upload → validate → restore, as a state machine |
| 10 | [11-02 Progress from a background migration](exercises/11-wpf/11-02-background-migration-progress/) | The host is a desktop app with a progress bar |

If you have time after those: [09-01](exercises/09-sdk-design/09-01-design-the-public-api/) (public API design),
[09-03](exercises/09-sdk-design/09-03-core-depends-on-ui/),
[09-05](exercises/09-sdk-design/09-05-evolving-the-api/),
[08-01](exercises/08-reliability/08-01-transient-server-errors/).

**Run it as a mock:** Session B in [INTERVIEW_MODE.md](INTERVIEW_MODE.md#session-b--sdk--migration) is the
closest to this job. Ask someone else to run it using `interviewer/mock-interviews/session-b-sdk-migration.md`.
The script gives the exact prompts, hints and timings, so anyone can run the session and keep the clock. Judging
the answers against its "Watch for" notes is easier for a developer; otherwise, compare against each exercise's
`SOLUTION.md` together afterwards.

### Design and judgement round

- [16-01 Design the migration SDK](exercises/16-system-design/16-01-design-the-migration-sdk/): talk it
  through for 20 minutes, using what is in [prep/sage-context.md](prep/sage-context.md).
- [16-05 Evolving the SDK](exercises/16-system-design/16-05-evolving-the-sdk/): shipping a DLL to machines
  you cannot update.
- [16-03 Migrating on a bad network](exercises/16-system-design/16-03-migrating-on-a-bad-network/) and
  [16-06 Moving to Azure](exercises/16-system-design/16-06-moving-to-azure/).
- The story bank: ownership, a disagreement, a production incident, and your first month.
- Questions to ask Sage: see [prep/sage-context.md](prep/sage-context.md#questions-to-ask-sage).

### MAUI

The job ad lists MAUI, but Sage's existing desktop products are .NET Framework applications. Know the WPF→MAUI
differences well enough to discuss them ([12-01](exercises/12-maui/12-01-detail-flow-navigation/),
[12-04](exercises/12-maui/12-04-platform-code-in-viewmodel/), quick-fire section G). If MAUI turns out to be
central, ask about it and go deeper afterwards.

---

## If time is short

- **Before Brillio:** quick-fire sections A–D and J, the two-minute intro, and 02-01.
- **Before Sage:** read the Sage context, then do 09-04, 07-01 and 10-04, and talk through 16-01 aloud.

## For every remote interview

- Test screen sharing, the microphone and your IDE the day before. Build the repository beforehand (a first
  restore takes minutes).
- Think out loud. Silence cannot be scored.
- Ask clarifying questions before designing anything. Say what you would measure before optimising anything.
- If the coding is in a browser editor, practise one or two exercises without IntelliSense first.
