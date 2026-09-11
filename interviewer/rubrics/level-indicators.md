# Weak / Mid-level / Strong / Senior Indicators

The general version of the "Scoring notes" table at the bottom of every `SOLUTION.md`. Use those first —
they are specific to the exercise. Use this when you need to place a response that the exercise notes do not
quite cover, or when writing up a debrief.

The target role is **mid-level**. "Senior" here means *beyond what the role requires*, and is worth recording
because it changes what the person could be given, not because its absence is a failure.

## Weak response

Indicates gaps that would show up in the first month.

- Changes code without a hypothesis; when it works, cannot say why.
- Treats symptoms: adds a delay, a retry, a `try`/`catch`, or a `lock` around the last line that threw.
- Does not distinguish "the test passes" from "the bug is fixed".
- Talks in rules (`async void` is bad, statics are bad) without the consequence behind the rule.
- Cannot describe an interleaving: says "race condition" as a label, not as a sequence of events.
- Silent for long stretches, or narrates keystrokes rather than reasoning.
- Does not ask about anything the exercise leaves ambiguous.

## Solid mid-level response

What the role actually needs.

- Reads the failing test first and treats it as the specification.
- Forms a hypothesis, checks it cheaply, and abandons it when the evidence says so.
- Finds the mechanism: *this* await is missing, *this* value is captured, *this* token is not passed on.
- Knows the standard tools and reaches for the right one: `SemaphoreSlim`/`Parallel.ForEachAsync` for
  bounded concurrency, `IProgress<T>` for progress, `CancellationToken` throughout, `TimeProvider` for time.
- Handles errors by category: fatal vs per-item, retryable vs not, and preserves the original exception.
- Writes or repairs a test that would have caught the bug, and can say why it is deterministic.
- Says what the customer experiences, not just what the code does.
- Asks for a hint rather than stalling, and explains what they had already ruled out.

## Strong response

Clearly above the bar; the person will pull the team up.

- Finds the cause quickly and then keeps going: "this also means X is wrong two calls up".
- Names the alternatives and picks one for a stated reason, including the option of not fixing it now.
- Thinks about the failure modes the exercise did not mention — partial failure, the second caller, the
  restart, the retry after the response was lost.
- Makes the untestable testable (injects the clock, the handler, the platform abstraction) rather than
  giving up on the test.
- Notices things about the *shape* of the code that made the bug possible, and says what would prevent a
  recurrence.
- Challenges a claim in the exercise or the PR description, with evidence.
- Communicates in a way that would work with a customer in the room.

## Senior-level response

Beyond the role. Record it; do not require it.

- Frames the problem as a product and operations question: what does support see, what does the customer
  believe, what do we tell them while we fix it.
- Reasons about scale and cost with arithmetic, not adjectives: round trips × latency, bytes × bandwidth,
  requests per uploaded file.
- Thinks about API evolution: what is now a contract, what will be expensive to change, and what should be
  `internal` until someone asks.
- Distinguishes binary, source and behavioural breaking changes without prompting.
- Designs for distributed failure by default: idempotency, reconciliation, server as the source of truth,
  the ambiguous result as a normal case.
- Chooses what *not* to build, and can defend the cut to both an engineer and a salesperson.
- Turns the fix into a guard: a test, an analyser, an architecture test, a metric with an alert.

## Where the line usually shows

These are the exercises where the mid/senior difference is clearest — worth using when a decision is close.

| Topic | Exercise | The question that separates them |
|---|---|---|
| Concurrency | 04-01, 04-02 | Do they avoid shared state, or guard it? |
| Retries | 08-01, 08-03 | Do they think about what 2,000 clients do at the same second? |
| API design | 09-01, 09-05 | Do they design for the second consumer and the next version? |
| SDK architecture | 16-01 | Do they say what is *not* in v1 and why? |
| Migration recovery | 10-03, 10-04 | Do they reconcile with the server, or trust local state? |
| Idempotency | 07-03, 10-04 | Is the key derived from identity and content, or random per attempt? |
| Performance | 14-01, 14-02 | Do they measure before changing, and know what the change costs? |
| Security | 06-02, 15-01 | Do they think about what ends up in a support bundle? |
| Distributed failure | 16-03 | Do they treat the lost response as normal rather than exceptional? |
