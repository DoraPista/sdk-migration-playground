# 08-02 Provisioning Takes Minutes – Interviewer Notes

**Type:** implementation (long-running operations) · **Time:** 25 min · **Solution code:** `code/src/ProvisioningClient.cs`
**Job relevance:** "provisioning services", "Azure/cloud architecture". This is the Azure REST async-operation pattern (`202` + `Operation-Location` / `Azure-AsyncOperation` + `Retry-After`).

## What is wrong

`ProvisionAsync` treats `202 Accepted` as success and deserializes the **operation** body as a `Destination`,
producing `Destination(null, null)`, which later becomes "Destination '' not found". `EnsureSuccessStatusCode` passes because 202 is a 2xx.

## Hints

1. "What does `202 Accepted` mean, as opposed to `201 Created`?"
2. "Where does the platform tell you where to look, and when?"
3. "Loop: wait (Retry-After) → GET Operation-Location → Succeeded/Failed/Running; with a deadline, cancellation and tolerance for transient poll errors."

## Intended solution (outline)

- 201/200 → the destination is ready.
- 202 → poll `Operation-Location`, honouring `Retry-After` (clamped to a sane range), until `Succeeded` (return the destination) or `Failed` (throw with the platform's message and `OperationId`).
- Deadline: 10 minutes, checked **before** sleeping; the last sleep is shortened to the remaining time.
- Transient poll errors (5xx, 429, network) → keep polling. The operation still runs server-side.
- Unknown statuses → keep polling (tolerant reader, see 05-02).
- All waits use the `TimeProvider` + token.

### Alternatives

- `IAsyncEnumerable<ProvisioningProgress>` / `IProgress<string>` to show "Creating storage… Configuring permissions…" in the wizard. Nice UX; ask whether the platform reports steps.
- Azure SDK style `Operation<T>` object (`WaitForCompletionAsync`, `UpdateStatusAsync`, `Id`) returned to the caller, so callers can persist the operation ID and resume waiting after an app restart. A **senior** answer to the closing question.
- Webhooks or push notifications instead of polling: not for a desktop client behind NAT.

## Common mistakes

- Polling in a tight loop (no delay) or with a fixed delay that ignores `Retry-After`.
- A deadline measured with `DateTime.Now` (untestable, DST) or checked only after sleeping.
- Throwing on the first 503 while polling, which abandons a running operation and orphans a half-created destination.
- Treating `HttpClient.Timeout` on the POST as "provisioning failed" (it may have been accepted; see 10-04 and 07-03).
- Forgetting cancellation in `Task.Delay`.

## The closing question

"What happens to the destination if the user closes the wizard while provisioning is running?"
The **server** keeps provisioning. When the wizard runs again, `POST /provision` returns `201` with the (now existing)
destination, which is why the 201 path matters. If it's still running: the platform should return the same operation (idempotent create),
or a 409. Good candidates reason about this without being told.

## Scoring notes

| Level | Indicators |
|---|---|
| Weak | Adds `Task.Delay(60000)` after the POST; or polls without hints; no deadline |
| Solid mid-level | Correct 201/202 handling, Retry-After polling, failure with operation ID, deadline, cancellation |
| Strong | Transient poll tolerance, clamped hints, deadline-before-sleep, unknown statuses tolerated |
| Senior | Operation handle for resume after restart, idempotent create semantics, progress UX, Azure LRO conventions |
