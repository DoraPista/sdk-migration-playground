# 05-02 The Server Upgrade That Broke Every Desktop – Interviewer Notes

**Type:** debugging / contract evolution · **Time:** 20 min · **Solution code:** `code/src/MigrationStatusClient.cs`

## What is wrong

1. **No `api-version` header.** The client gets the *latest* version (v2), which has a new state (`Verifying`) and a
   different shape (`progress` object). The platform told everyone to pin; we didn't.
2. **Strict reader.** `UnmappedMemberHandling.Disallow` turns every *additive* server change into a crash. Additive
   changes are explicitly allowed by the platform's contract. A **tolerant reader** is the right default for clients.
3. **Enum brittleness.** `JsonStringEnumConverter` throws on unknown values. Even with pinning, the March hotfix sends
   `Paused`. Defence in depth: map unknown values to something the code can reason about.
4. **Polling loop dies** on the first exception (no resilience at all), and it never ends if the migration fails
   server-side. Worth discussing, though not tested here.

## Hints

1. "Compare what the server sends to a request with and without the `api-version` header."
2. "Which of the platform's changes are *allowed* by its contract, and which of them does our client reject?"
3. "Send the version header; stop rejecting unknown properties; decide what an unknown enum value becomes."

## Intended solution

- `api-version: 1` on every request (per request, not by mutating `DefaultRequestHeaders`).
- Default `JsonSerializerOptions` (web defaults): unknown members are ignored.
- A tolerant converter: unknown or numeric state strings map to `MigrationState.Unknown`, and the poller treats Unknown as "not finished".
- `Unknown = -1` so **existing numeric values don't shift**. Enum values are compiled into consumer binaries,
  so renumbering is a *binary* breaking change for SDK consumers. Great versioning discussion (see 09-05).

### Alternatives

- Keep `State` as a string plus a helper `IsTerminal`. Very tolerant; loses type safety for consumers.
- A `readonly record struct MigrationState(string Value)` with well-known static instances (the "extensible enum" pattern
  used by Azure SDKs). An excellent senior answer, because new server values then flow through to consumers without an SDK release.
- `[JsonStringEnumMemberName]` + a fallback converter. Fine.

## Common mistakes

- Only adding the header: the `Paused` test still fails (not defence in depth).
- Adding `Unknown` as the *first* member without an explicit value, which renumbers `Created`/`Uploading`/`Completed`.
- `Enum.TryParse("5")` succeeds for numeric strings; `IsDefined` alone isn't enough if the server ever sends numbers.
- Catching `JsonException` in the poller and continuing forever, so real contract breaks are hidden.

## Follow-ups

- "What should *our* SDK promise consumers about enum values?" (Document that new values may appear; tell consumers to have a default branch; or use extensible enums.)
- "The migration fails on the server. How does `WaitForCompletionAsync` end?" (It doesn't. Terminal states need to be part of the contract; add `Failed` in v2 and upgrade deliberately.)
- "How would you catch contract drift *before* customers do?" (Contract tests against the platform's OpenAPI; a canary client with a strict reader in CI, never in production.)
- "Should the SDK poll at all?" (Webhooks/SignalR/long-polling; `Retry-After` on status responses; backoff while nothing changes.)

## Scoring notes

| Level | Indicators |
|---|---|
| Weak | "Tell the platform team to revert"; removes the enum entirely; doesn't read the guidelines in the README |
| Solid mid-level | Pins the version; tolerant reader; unknown enum handled; tests pass |
| Strong | Explicit enum numbering and why; explains strict vs tolerant readers; notes the poller's lack of terminal/failure handling |
| Senior | Extensible enums; contract testing strategy; the SDK's own compatibility promises to its consumers |
