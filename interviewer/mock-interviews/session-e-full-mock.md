# Session E – Full Mock (90 minutes)

**Shape:** debugging → C# modelling → async → concurrency → reliability → UI → architecture.
**Reads:** the whole role in one sitting. The extra thing this session measures that A–D do not is
**consistency**: the same person should debug methodically at 0:05 and still reason clearly at 1:20.

**Prepare:** read all seven `SOLUTION.md`/`DISCUSSION.md` files. Build the day before — including the WPF or
MAUI project you plan to use at 1:02. Decide in advance where you will cut if you run long (see the end).

Keep the pace. If an exercise is not going anywhere by its two-thirds mark, give hint 3 or move on; the
information is in the *range* of exercises, not in finishing any one of them.

---

## 0:00–0:05 Introductions

> "Ninety minutes, seven short pieces, ending with a design discussion. I'll move you on before you're
> finished — that's normal and not a bad sign. Think out loud, look anything up, and ask for hints."

---

## 0:05–0:15 · 02-02 The failed migration that reported success

**Exercise:** `exercises/02-debugging/02-02-failed-migration-reports-success/` · Medium · [notes](../solutions/02-debugging/02-02-failed-migration-reports-success/SOLUTION.md)

**Prompt**

> "The customer's external drive was disconnected halfway through. The app reported the migration as
> successful. Find out how that is possible."

**Hints**

1. "What does `Scan` return when the drive is disconnected? How would the caller know?"
2. "Go through each `catch`: what is it protecting against, and what does it hide?"

**Follow-ups:** "Which of these `catch` blocks would you keep?" · "What should the user have seen?"

**Watch for:** reading every `catch` rather than the first one; distinguishing fatal from per-file failures;
noticing cancellation being swallowed.

**Scores:** debugging methodology, error handling.

---

## 0:15–0:25 · 01-03 "Provisioning failed. Please contact support."

**Exercise:** `exercises/01-csharp/01-03-provisioning-results/` · Medium · [notes](../solutions/01-csharp/01-03-provisioning-results/SOLUTION.md)

**Prompt**

> "Support get this message several times a week and can never tell what actually went wrong. The API does
> return the reason. Redesign what this method gives its caller."

**Hints**

1. "List every outcome `ProvisionAsync` can have. How does a caller tell them apart today?"
2. "What does 409 mean for *this* operation, and what is in the body?"

**Follow-ups:** "Exception or return value — when is each right?" · "How does the UI show the difference
between 'already exists' and 'quota exceeded'?" · "What does this cost a caller who doesn't care?"

**Watch for:** enumerating outcomes before designing; a shape that makes the caller handle each case;
awareness that exceptions for expected outcomes are a design smell.

**Scores:** API design, error handling, communication.

---

## 0:25–0:37 · 03-01 Five hundred files at once

**Exercise:** `exercises/03-async/03-01-five-hundred-files/` · Medium · [notes](../solutions/03-async/03-01-five-hundred-files/SOLUTION.md)

**Prompt**

> "This uploads a project's files. With 500 files the platform starts returning 429 and the customer's VPN
> falls over. The test asserts how many uploads may be in flight."

**Hints**

1. "How many uploads are running 1 ms after `UploadAllAsync` is called?"
2. "What would limit how many of those lambdas are allowed to *start*?"

**Follow-ups:** "What number would you choose, and how would you find out?" · "The limit should be
configurable — whose decision is it?" · "What do you do with the 429 you still get?" (Bridges to 08-01.)

**Watch for:** knowing that `Select(async …)` starts everything immediately; picking a mechanism rather than
guessing; treating the limit as a measured value rather than a magic number.

**Scores:** concurrency awareness, correctness, trade-off awareness.

---

## 0:37–0:47 · 04-02 "Start migration" clicked twice

**Exercise:** `exercises/04-concurrency/04-02-start-clicked-twice/` · Medium · [notes](../solutions/04-concurrency/04-02-start-clicked-twice/SOLUTION.md)

**Prompt**

> "Double-clicking Start creates two migrations for the same customer. Disabling the button in the UI was
> tried and it still happens. The test reproduces it deterministically."

**Hints**

1. "What is `_current` during the three seconds that `CreateMigrationAsync` takes?"
2. "What could you store *immediately*, before the slow call finishes, that a second caller could wait on?"

**Follow-ups:** "Why isn't disabling the button enough?" · "What if the second click comes from a different
process?" (Server-side idempotency — bridges to 07-03.) · "What happens to the stored task if the start
fails?"

**Watch for:** identifying the check-then-act window across the `await`; storing the task rather than a
boolean; remembering to clear it on failure. A `lock` held across an `await` is the classic wrong answer —
let them try it and watch them notice.

**Scores:** concurrency awareness, correctness, testing.

---

## 0:47–1:02 · 08-01 The platform has bad minutes

**Exercise:** `exercises/08-reliability/08-01-transient-server-errors/` · Medium · [notes](../solutions/08-reliability/08-01-transient-server-errors/SOLUTION.md)

**Prompt**

> "The platform has a bad minute every few hours: 500s, 503s with `Retry-After`, the odd timeout. Right now a
> migration dies. Make it survive — the tests define what 'survive' means."

**Hints**

1. "Which of these status codes could possibly succeed if you sent the same request again?"
2. "If 2,000 desktops all get a 503 at the same second, what does a fixed 1-second retry do?"

**Follow-ups:** "Which codes do you never retry, and why?" · "Where does the jitter come from?" · "When do
you stop retrying altogether?" · "What does the user see during all this?"

**Watch for:** classification before mechanism; honouring `Retry-After`; jitter; a budget or a cap. Knowing
that retrying a 400 or a 401 is worse than useless.

**Scores:** error handling, reasoning, trade-off awareness, testing.

---

## 1:02–1:15 · UI slot — 11-02 *or* 12-02

Ask which they are stronger in and use that one; the point is to see UI-thread reasoning, not to test the
framework they use less.

- **WPF:** `exercises/11-wpf/11-02-background-migration-progress/` — [notes](../solutions/11-wpf/11-02-background-migration-progress/SOLUTION.md) ·
  hint 1: "Which thread raises `FileUploaded`, and which thread is allowed to touch `Files`?"
- **MAUI:** `exercises/12-maui/12-02-migration-list-not-updating/` — [notes](../solutions/12-maui/12-02-migration-list-not-updating/SOLUTION.md) ·
  hint 1: "Four symptoms — do not assume one cause."

**Follow-up either way:** "Should the SDK know about the UI thread at all? Who should do the marshalling?"

**Watch for:** the same concurrency reasoning as earlier in the session, applied to a UI. Inconsistency here
is informative — some candidates reason well about servers and stop thinking at the view.

**Scores:** concurrency awareness, correctness, API design.

---

## 1:15–1:30 · 16-03 Migrating on a bad network

**Exercise:** `exercises/16-system-design/16-03-migrating-on-a-bad-network/` · Expert Discussion · [notes](../solutions/16-system-design/16-03-migrating-on-a-bad-network/DISCUSSION.md)

Close on a discussion — no code, and it lets a tired candidate show judgement rather than typing.

**Prompt**

> "Site office, shared 4G, drops for a minute several times an hour, one request in fifty comes back 502,
> and the VPN restarts at five o'clock. A 20 GB migration over three days, laptop closed each evening.
> Design the client for that network."

**Hints**

1. "One request in fifty fails. How many failures does this migration see in total?"
2. "Which failures are worth retrying, and which ones mean it may already have worked?"

**Follow-ups:** "The response was lost but the chunk arrived — how does the client find out?" · "Everything
fails at 17:00 and recovers at 17:01. What does your client do?" · "What does the consultant see on screen
while all this happens?" · "When do you give up, and what do you leave behind?"

**Watch for:** the connection back to everything else in the session — classification (08-01), idempotency
(04-02's follow-up), the ambiguous result, and honest progress. A strong candidate will refer to their own
earlier answers; that is exactly the consistency this session measures.

**Scores:** reasoning, distributed-failure awareness, communication, trade-off awareness.

---

## Scoring sheet

| Time | Exercise | Dimensions | Hint level | Evidence |
|---|---|---|---|---|
| 0:05 | 02-02 | debugging, error handling | | |
| 0:15 | 01-03 | API design, error handling, communication | | |
| 0:25 | 03-01 | concurrency, correctness, trade-offs | | |
| 0:37 | 04-02 | concurrency, correctness, testing | | |
| 0:47 | 08-01 | error handling, reasoning, trade-offs, testing | | |
| 1:02 | 11-02 / 12-02 | concurrency, correctness, API design | | |
| 1:15 | 16-03 | reasoning, distributed failure, communication | | |

**Reading the session as a whole**

- **Consistent 3s** across seven very different problems is a strong signal for this role — more than two 4s
  and three 2s.
- **Compare 0:25 and 1:02.** Concurrency reasoning that evaporates at the UI layer is a common and important
  pattern.
- **Compare 0:47 and 1:15.** Did the retry policy they wrote at 0:47 survive the scenario at 1:15? If they
  notice the tension themselves, that is a 5 for reasoning.
- **Fatigue is data**, but do not over-read the last twenty minutes of a ninety-minute session.

**If running long:** cut 01-03 (its material overlaps 02-02) and shorten the UI slot to ten minutes. Protect
16-03 — it is the best single window on judgement in the whole repository.
