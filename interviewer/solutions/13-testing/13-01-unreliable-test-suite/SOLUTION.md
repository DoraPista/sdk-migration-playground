# 13-01 The Test Suite Nobody Trusts – Interviewer Notes

**Type:** test quality · **Time:** 25 min · **Solution code:** `code/tests/ChunkedUploaderTests.cs`

## Hints

1. "Run the suite twice in a row. Then run the failing test on its own."
2. "What do these tests share with each other, and what do they wait for?"
3. "Remove the static state, give each test its own uploader and temp files, replace the sleeps with a fake clock and gates, assert instead of catching, and delete the test that asserts nothing."

## What is wrong with each test

| Test | Problems |
|---|---|
| `Test1` | Name says nothing; writes to `static` fields (`_uploadCounter`, `_lastHash`) so it leaks state into other tests |
| `Test2_Hash` | **Order-dependent**: it asserts on state produced by `Test1`. xUnit does not guarantee order (and runs classes in parallel). It is also `async` with nothing to await |
| `UploadWorks` | `try { … } catch (Exception) { }` around the assertions: **this test can never fail**. It was green during the week the uploader returned null |
| `RetryTest` | `Task.Delay(5s)` plus a 2-second retry delay: 5+ seconds of wall clock, and flaky under load. The uploader already accepts a `TimeProvider`; nobody used it |
| `IntegrationTest` | Hits `http://localhost:5999`: fails offline, passes only on the machine that happens to run something there. Not a test, a trap |
| `ProgressTest` | `Progress<T>` posts to the thread pool (or captured context), so the assertion races the callback; "give it a moment" is a sleep by another name. Asserts only `> 0` |
| `CancelTest` | Swallows the expected exception and asserts **nothing**. Timing-based cancellation (150 ms vs 200 ms) is a coin flip |
| `OptionsTest` | Tests the framework, not the product: default property values and `Assert.NotNull` on something just constructed |

## What a good rewrite looks like

- One behaviour per test, named after the behaviour (`A_transient_failure_retries_the_same_chunk`).
- Per-test instances instead of statics; no shared mutable state.
- `FakeTimeProvider` (already supported by the uploader) so retries cost microseconds.
- A synchronous `IProgress<T>` implementation, so progress assertions are exact (`[1000, 2000, 3000]`).
- Deterministic cancellation: cancel **from the fake handler** at a known point, then assert the exception *and* that the remaining chunks were not sent.
- `[Theory]` for the status-code families (transient vs permanent) instead of one example each.
- Assertions on observable behaviour: request count, offsets, body hashes, result values.

The rewritten suite runs in well under a second and has no ordering, network or timing dependencies.

## Discussion points

- "Which of these tests would you delete outright?" (`OptionsTest`, and `IntegrationTest` in this form. Keeping a *real* integration test is fine, but against a controlled server, as exercise 05-01 does with the in-process mock.)
- "Is a test that cannot fail worse than no test?" (Yes: it buys false confidence and costs maintenance.)
- "How would you stop sleeps creeping back in?" (Time abstraction in the product code, a review rule, or a test-time analyzer; CI that fails if the suite gets slower.)
- "xUnit parallelism: what are the rules?" (Different classes run in parallel; tests inside a class are sequential; static state is shared across all of it.)
- "How would you find flaky tests in CI?" (Repeat runs, flaky-test detection in the test platform, quarantine + fix policy.)

## Common mistakes

- Keeping the sleeps but shortening them.
- Replacing the static fields with a constructor-shared field but keeping the cross-test dependency.
- Rewriting everything into one giant test.
- Mocking `HttpClient` with a hand-rolled subclass rather than the message handler (also fine, but ask why).
- Asserting on log text or on internals instead of behaviour.

## Scoring notes

| Level | Indicators |
|---|---|
| Weak | Renames tests, keeps sleeps, doesn't notice the swallowed assertions or the order dependency |
| Solid mid-level | Removes statics, sleeps and the network test; uses the TimeProvider; clear names; meaningful assertions |
| Strong | Deterministic cancellation, theories for status families, exact progress assertions, explains each removal |
| Senior | Talks about trust in the suite as a team-level property: policy on flaky tests, what integration tests are for, and how to keep the suite fast as it grows |
