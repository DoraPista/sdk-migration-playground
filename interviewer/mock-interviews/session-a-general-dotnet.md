# Session A – General .NET (60 minutes)

**Shape:** C# semantics → a debugging ticket → an API compatibility problem → a code review.
**Reads:** can this person work in unfamiliar C#, find a real bug, integrate with a moving API, and review
a colleague's work without being unpleasant about it?

**Prepare:** build the day before; `git checkout -- exercises/`; have
`interviewer/rubrics/scoring-model.md` open. Read the four `SOLUTION.md` files.

---

## 0:00–0:05 Introductions

> "We'll do four short pieces: a C# bug, a support ticket, an API problem and a pull request review.
> Think out loud — I care more about how you get there than whether you finish. You can look anything up;
> just tell me what you're looking for. Ask for a hint whenever you want one, it costs you nothing."

Check `dotnet test` runs once so no time is lost later.

---

## 0:05–0:15 · 01-01 Project totals are wrong

**Exercise:** `exercises/01-csharp/01-01-project-totals-wrong/` · Easy · [notes](../solutions/01-csharp/01-01-project-totals-wrong/SOLUTION.md)

**Prompt**

> "The results file from a migration says one project has 12 files and 40 MB. The portal shows 31 files.
> The tests here reproduce it. Find out why."

**Hints**

1. "Compare the project IDs in the results file with what the portal shows."
2. "What is the key of your dictionary, and how does the file store it? And how many lines can one file have?"
3. See the notes — give only if they are still lost at 0:12.

**Follow-ups**

- "Where else in a codebase like this would the same mistake be waiting?"
- "How would you stop it coming back?" (A test on the grouping key; a type instead of a raw string.)

**Watch for:** whether they read the test first; whether they say "case sensitivity" as a guess or check it;
whether they notice the second cause after finding the first. Finding one of the two and stopping is the
common outcome, and is a 3 if the reasoning is sound.

**Scores:** correctness, reasoning, testing.

---

## 0:15–0:27 · 02-01 The migration finishes too early

**Exercise:** `exercises/02-debugging/02-01-migration-finishes-too-early/` · Medium · [notes](../solutions/02-debugging/02-01-migration-finishes-too-early/SOLUTION.md)

**Prompt**

> "Support ticket: 'the app says the migration is complete, but files keep arriving on the platform for
> another two minutes, and sometimes a few never arrive at all.' It is reproducible in the test."

**Hints**

1. "When exactly does `_queue.IsEmpty` become true?"
2. "What happens to the `Task` returned by `Task.Run` here? What happens to an exception thrown inside it?"
3. See the notes.

**Follow-ups**

- "Files sometimes never arrive. Same cause or different?"
- "How would you have found this without the test — what would you have added to the app?"
- "Would you have caught this in review?"

**Watch for:** whether they separate the two symptoms (early completion, lost files); whether they know
where the exception from a fire-and-forget task goes; whether the fix keeps per-file failures from killing
the run.

**Scores:** correctness, debugging methodology, error handling, concurrency awareness.

---

## 0:27–0:40 · 05-02 The server upgrade that broke every desktop

**Exercise:** `exercises/05-http-api/05-02-server-upgrade-breaks-client/` · Medium · [notes](../solutions/05-http-api/05-02-server-upgrade-breaks-client/SOLUTION.md)

**Prompt**

> "The platform deployed on Tuesday. Since then every desktop client throws while reading migration status.
> The platform team say they made no breaking changes. Who is right?"

**Hints**

1. "Compare what the server sends to a request with and without the `api-version` header."
2. "Which of the platform's changes are *allowed* by its contract, and which of them does our client reject?"
3. See the notes.

**Follow-ups**

- "The platform team ask what they could have done differently. What do you say?"
- "A new enum value arrives that you have never heard of. What should the client do — and what should it
  show the user?"
- "How do you ship the fix to 200 laptops you do not control?" (Opens the door to 16-05.)

**Watch for:** whether they blame the server reflexively; whether they know what a tolerant reader is;
whether they treat the unknown enum value as a real decision rather than an exception.

**Scores:** reasoning, error handling, API design, trade-off awareness.

---

## 0:40–0:58 · 15-01 Review this PR: bulk upload

**Exercise:** `exercises/15-code-review/15-01-review-bulk-upload/` · Medium · [notes](../solutions/15-code-review/15-01-review-bulk-upload/SOLUTION.md)

**Prompt**

> "A teammate opened this PR and wants to merge today — there's a customer demo on Thursday. Read `PR.md`
> and the two files. Take about eight minutes, then talk me through it. I'm the author and I'm in the room."

Let them read in silence. At 0:48, ask them to start talking whether they are finished or not.

**Hints**

1. "Decide what you would say if I were about to merge in five minutes."
2. "Look at what is static, what is written to the console, what happens when two uploads finish at the same
   moment, and what that retry loop retries."
3. Point at the `Console.WriteLine` with the user and password, then ask what else in the file has the same
   character.

**Follow-ups**

- "If you could only leave one comment, which?"
- "Which of these block the merge, and which are 'raise it, ship it'?"
- "I wrote in the description that the `using` on `HttpClient` prevents leaks. Is that right?"
- "I said it's hard to test because of the HTTP calls. Agree?"
- "What is good in this PR?"

**Watch for:** prioritisation over enumeration; effects rather than rules; tone (these comments are being
said to the author's face); whether they accept the three confident, wrong claims in the description.

**Scores:** communication, code quality, concurrency awareness, security awareness, trade-off awareness.

---

## 0:58–1:00 Close

> "Anything you want to ask me?"

Then, while it is fresh, write the scores and one sentence of evidence each.

## Scoring sheet

| Exercise | Dimensions | Hint level reached | Evidence |
|---|---|---|---|
| 01-01 | correctness, reasoning, testing | | |
| 02-01 | correctness, debugging, error handling, concurrency | | |
| 05-02 | reasoning, error handling, API design, trade-offs | | |
| 15-01 | communication, code quality, concurrency, security, trade-offs | | |

**Signals of a solid mid-level session:** both causes found in at least one of 01-01 and 02-01; the missing
`await`/lost-task mechanism explained precisely; the client's share of blame in 05-02 accepted; six or more
real findings in the review, ranked, phrased as they would say them to a colleague.

**Substitutions** if something is already familiar to the candidate: 01-01 → 01-02, 02-01 → 02-05,
05-02 → 05-01 (longer; drop the review to 12 minutes), 15-01 → 09-02.
