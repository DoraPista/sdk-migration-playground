# 01-03 "Provisioning Failed" – Interviewer Notes

**Type:** result modelling / refactoring · **Time:** 25 min · **Solution code:** `code/src/`

## What is wrong

| Situation | Current behaviour | Why it's wrong |
|---|---|---|
| 409 already provisioned | `null` → "contact support" | 409 here means the thing you asked for **exists**. For a create-if-missing operation that is success, and the body carries the ID |
| Network failure | returns `"-1"` → treated as a destination ID | A magic value in the success channel. Nothing forces callers to check it |
| 429 | `"RETRY"` | Magic value; `Retry-After` is lost |
| 5xx | `EnsureSuccessStatusCode` throws `HttpRequestException` | Step doesn't catch it, so the wizard crashes; the exception type doesn't say "retryable" |
| 400 | `ArgumentException` with raw JSON | Wrong exception type (the *server* rejected the request; no argument was wrong); message isn't user-friendly |
| Timeout | `TaskCanceledException` propagates | Indistinguishable from user cancellation |
| Response disposal | `HttpResponseMessage` never disposed | Minor leak |

## Hints

1. "List every outcome `ProvisionAsync` can have. How does a caller tell them apart today?"
2. "What does 409 mean for *this* operation, and what is in the body?"
3. "What if the method returned a type with one case per outcome, so the compiler makes the caller handle each?"

## Intended solution

A closed set of outcomes (see `ProvisioningOutcome`): `Provisioned(id, alreadyExisted)`, `Rejected(reason, errors)`,
`TransientFailure(reason, retryAfter, exception)`. The step maps each case to the UI contract with a `switch`.
Cancellation still throws `OperationCanceledException`. An HttpClient timeout is **not** treated as user cancellation.

### Alternative valid solutions

- **Exceptions with a hierarchy** (`ProvisioningRejectedException`, `TransientProvisioningException`) plus a normal return
  for success. Perfectly defensible, especially as SDK public API (idiomatic .NET). The key is that the *types* carry
  the semantics, not magic values.
- A generic `Result<T, TError>` type. Fine, but watch for over-engineering. Ask how they would evolve it.
- `bool TryProvision(out string id)` style: rejectable; it loses the reason.

A good discussion: **expected outcomes vs exceptional ones.** 409 and 400 are expected business outcomes
(return values). A socket failure is arguably exceptional, but the caller must handle it anyway, so a result is reasonable.

## Common mistakes

- Treating every 409 as success. The solution checks for a `destinationId` in the body; a 409 for a
  different reason (e.g. customer suspended) stays a rejection.
- Catching `Exception` in the step and returning `CanRetry = true`. That swallows cancellation and programming errors.
- Throwing on cancellation *and* catching `TaskCanceledException` everywhere, which turns timeouts into cancellations or the reverse.
- Keeping the magic strings but naming them as constants ("it's fine now").

## Edge cases to discuss

- The 409 body is malformed or missing the ID.
- A 201 without a body.
- 403 (the tenant isn't allowed to provision): permanent, but the message should differ from validation.
- Retry-After: should the step honour it, or just surface it to the UI?

## Follow-ups

- "Is `ProvisioningStepResult` a good public contract?" (A boolean plus nullable fields allows invalid states like `Succeeded = true` with a null ID. Would they change it given the chance?)
- "Would you make this call idempotent? How?" (Treat 409-with-ID as success, which is already done; or use an idempotency key.)
- "How does the UI decide whether to show 'Retry' vs 'Contact support'?"

## Scoring notes

| Level | Indicators |
|---|---|
| Weak | Adds more magic values or `if (id == "-1")`; catches `Exception`; doesn't notice 409 means success |
| Solid mid-level | Explicit outcome type or exception hierarchy; 409 handled with the body's ID; transient vs permanent separated; cancellation preserved |
| Strong | Distinguishes timeout from cancellation; keeps Retry-After; disposes responses; explains exceptions vs results for SDK callers |
| Senior | Discusses the public contract's invalid states, versioning a closed hierarchy, idempotent-create semantics across the platform, and observability (logging the reason and correlation ID) |
