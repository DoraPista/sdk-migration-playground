# 02-05 Uploads Go to the Wrong Customer – Interviewer Notes

**Type:** debugging (DI lifetimes) · **Time:** 25 min · **Solution code:** `code/`

## What is wrong

| Registration | Holds | Effect |
|---|---|---|
| `AddSingleton<UploadTarget>()` | `_destinationId` cached on first use | Incident 1: every later job reuses the **first** customer's destination |
| `AddSingleton<CustomerSession>()` | "current customer" | Incident 2: concurrent jobs overwrite each other's customer. Job A signs in, awaits, job B signs in, job A continues as B |

The app *does* create a scope per job, which is exactly why the bug is surprising. Singletons ignore scopes.

**The trap:** changing only `CustomerSession` to `Scoped` leaves `UploadTarget` (singleton) depending on it.
MS DI resolves a singleton's dependencies from the **root** scope, so `UploadTarget` captures the root's
`CustomerSession`, which is never signed in, and you get "No customer is signed in". This is the captive dependency problem.
With `ValidateScopes = true` the container throws at resolution time instead.

## Hints

1. "Which objects in this design hold per-customer state?"
2. "What does `CreateScope()` do for a service registered with `AddSingleton`?"
3. "Match each lifetime to the data the service holds, and check nothing long-lived depends on something short-lived."

## Intended solution

`CustomerSession` and `UploadTarget` → `Scoped`. Optionally `CreateAsyncScope()`. Add a test building the provider with
`ValidateScopes`/`ValidateOnBuild` (see `code/tests/RegistrationValidationTests.cs`).

### Alternatives

- Remove ambient state entirely: `MigrationJob.RunAsync(customerId, …)` passes the customer explicitly to
  `UploadTarget.GetDestinationIdAsync(customerId)`, which caches per customer in a `ConcurrentDictionary<string, Task<string>>`.
  Arguably the **best** design: explicit data flow beats ambient "current customer" state, especially in an SDK.
- An `AsyncLocal<CustomerContext>`-based accessor (like `IHttpContextAccessor`). Works, and is harder to reason about. Worth discussing the trade-off.

## Common mistakes

- Only fixing `UploadTarget` (sequential test passes, concurrent test fails).
- Only fixing `CustomerSession`, which hits the captive-dependency exception. See whether they can diagnose it from the error.
- Making everything `Transient`: `UploadTarget` then re-queries the directory in every consumer and `CustomerSession` no longer shares state within a job (sign-in lost).
- Adding a `lock` around `RunAsync`. It "fixes" concurrency by serialising all customers, and the sequential bug remains.

## Follow-ups

- "Why didn't this fail in the single-customer version?"
- "How would you catch it at startup?" (`ValidateOnBuild`, `ValidateScopes`, which are on by default for ASP.NET Core in Development only.)
- "If the SDK exposes `AddMigrationKit`, what does it owe its consumers?" (Documented lifetimes; never register per-operation state as singleton; perhaps avoid ambient state in the public API.)
- "What's the security impact?" (Cross-tenant data exposure: a reportable incident, not just a bug.)

## Scoring notes

| Level | Indicators |
|---|---|
| Weak | Adds locks; can't explain singleton vs scoped; changes lifetimes by trial and error |
| Solid mid-level | Identifies both registrations; explains scopes; fixes both; passes both tests |
| Strong | Explains captive dependencies and root-scope resolution; adds validation; recognises the security severity |
| Senior | Proposes removing ambient state from the SDK design; discusses what an SDK's DI extension should guarantee and document |
