# Session B – SDK / Migration (75 minutes)

**Shape:** design the API → a workflow bug → the hardest transfer problem → the ambiguous failure → scale.
**Reads:** the session closest to the actual job. Can this person design a library other people will use,
and does their design survive the things that go wrong in the field?

**Prepare:** read all five `SOLUTION.md`/`DISCUSSION.md` files. Have a whiteboard or a blank file for the
first item. Build the day before.

---

## 0:00–0:20 · 09-01 Design the SDK's public API

**Exercise:** `exercises/09-sdk-design/09-01-design-the-public-api/` · Hard · [notes](../solutions/09-sdk-design/09-01-design-the-public-api/SOLUTION.md)

**Prompt**

> "Read `docs/requirements.md` — take five minutes. Then design the public API of the SDK out loud, writing
> signatures as you go. Two apps will use it: our WPF desktop app and a MAUI app. I'll interrupt with
> questions."

Do **not** show them `reference/PublicApi.cs`. Use it to compare against.

**Hints**

1. "Start at the call site: write the ten lines a WPF developer types to run a migration."
2. "What does the caller need after starting — the id, progress, cancellation, the result? A bare `Task`
   gives them none of those."
3. See the notes.

**Follow-ups** (ask as they become relevant, not all)

- "Where does cancellation come from when the Cancel button is a long way from the code that started it?"
- "A migration finishes with 40 files uploaded and 3 failed. Is that an exception?"
- "Which thread raises progress, and who says so?"
- "What is *not* in v1?"
- "What does the MAUI app need that the WPF app does not?"

**Watch for:** an instance client built from options rather than a static; a handle/run object with an id;
progress and cancellation designed in rather than added; failure as a result. Whether they keep UI concerns
out of the library without being told.

**Scores:** API design, trade-off awareness, communication, maintainability.

---

## 0:20–0:35 · 10-01 A migration that says "Completed"

**Exercise:** `exercises/10-migration/10-01-migration-workflow-states/` · Hard · [notes](../solutions/10-migration/10-01-migration-workflow-states/SOLUTION.md)

**Prompt**

> "A customer's migration says Completed in the portal. Three of their files are not there. The workflow
> code is here and the tests reproduce it."

**Hints**

1. "Follow what happens to the result of each stage: validation, uploads, verification, completion."
2. "Which of these stages can fail without anyone noticing?"
3. See the notes.

**Follow-ups**

- "What states does a migration actually need? Which does this code not have?"
- "Should 'completed with 3 failures' be Completed, Failed, or something else? Who decides?"
- "The app crashes during the upload stage. What state is the migration in when it restarts?" (Bridges to 10-03.)

**Watch for:** whether they treat the state machine as the design problem rather than patching one `if`;
whether they distinguish "the stage ran" from "the stage succeeded"; whether they ask who consumes the state.

**Scores:** correctness, error handling, API design, reasoning.

---

## 0:35–0:55 · 07-01 The 100 GB file

**Exercise:** `exercises/07-file-transfer/07-01-large-file-upload/` · Medium · [notes](../solutions/07-file-transfer/07-01-large-file-upload/SOLUTION.md)

**Prompt**

> "This uploads a file to the platform. It works in our tests. A customer has survey scans of 40 to 100 GB
> and the app dies. The test here asserts the memory it is allowed to use."

**Hints**

1. "How much memory does line 1 of `UploadAsync` need for a 100 GB file?"
2. "The server needs the hash *before* the body. What does that force you to do if you can't hold the file
   in memory?"
3. See the notes.

**Follow-ups**

- "Two passes over 100 GB on a slow disk. Is that acceptable? What would you do instead?"
- "How do you report progress without a callback per byte?" (Bridges to 11-02/14-02.)
- "The connection drops at 80 GB." (Bridges to 07-02.)

**Watch for:** whether they reach for streaming immediately; whether they notice the hash-before-body
constraint; whether the progress reporting they add would flood a UI. A candidate who says "I'd measure the
allocation rather than guess" earns it here.

**Scores:** correctness, reasoning, performance thinking (score under code quality/trade-offs), testing.

---

## 0:55–1:05 · 10-04 The upload that might have worked

**Exercise:** `exercises/10-migration/10-04-lost-upload-response/` · Expert Discussion · [notes](../solutions/10-migration/10-04-lost-upload-response/SOLUTION.md)

Discussion only — no code. Run the demo once before the session so you know what it prints.

**Prompt**

> "A chunk upload throws `HttpRequestException` after the request was sent. What are all the things that
> could have happened on the platform, and what does your client do next?"

**Hints**

1. "List every state the world could be in after that exception, not just the two obvious ones."
2. "The client cannot tell those states apart from where it is standing. Who can?"

**Follow-ups**

- "You retry and the platform says 409. What does that mean, and what do you do?"
- "Where does the idempotency key come from? What happens if it is a new GUID per attempt?"
- "The app crashed between uploading and recording. What does the next run do?"

**Watch for:** enumerating the states rather than assuming failure; server-as-source-of-truth; an
idempotency key derived from identity and content.

**Scores:** reasoning, concurrency/distributed-failure awareness, trade-off awareness.

---

## 1:05–1:15 · 16-02 From 100 MB to 100 GB

**Exercise:** `exercises/16-system-design/16-02-from-100mb-to-100gb/` · Expert Discussion · [notes](../solutions/16-system-design/16-02-from-100mb-to-100gb/DISCUSSION.md)

**Prompt**

> "Sales signed a customer with 100 GB across 1.2 million files, live in eight weeks. Today's code has been
> planning their export for two days without finishing. What breaks first, and what do you do about it?"

**Hints**

1. "Which *assumption* breaks first? Not which code is slowest."
2. "Do the arithmetic out loud: 1.2 million files at one extra round trip each."

**Follow-ups**

- "What can't you fix in eight weeks, and what do you tell the customer?"
- "How do you test a change without a two-day run?"
- "Which single change buys the most?"

**Watch for:** ordering; arithmetic rather than adjectives; separating feasibility from optimisation;
whether expectation management appears at all.

**Scores:** trade-off awareness, reasoning, communication.

---

## Scoring sheet

| Exercise | Dimensions | Hint level | Evidence |
|---|---|---|---|
| 09-01 | API design, trade-offs, communication, maintainability | | |
| 10-01 | correctness, error handling, API design, reasoning | | |
| 07-01 | correctness, reasoning, testing | | |
| 10-04 | reasoning, distributed failure, trade-offs | | |
| 16-02 | trade-offs, reasoning, communication | | |

**Signals of a solid mid-level session:** an API with a handle, progress and cancellation; the workflow's
"stage ran ≠ stage succeeded" distinction found; streaming reached without a level-3 hint; the ambiguous
result recognised as normal rather than exceptional; a sensible ordering in 16-02.

**Signals above the bar:** designing 09-01 for the second consumer and saying what is out of v1; connecting
10-04 to 07-03's idempotency key unprompted; doing the bandwidth arithmetic in 16-02.

**If running short:** drop 16-02 and give 10-04 the full 20 minutes. **If running long:** 09-01 is the one to
cut short — stop it at a usage snippet plus four signatures.
