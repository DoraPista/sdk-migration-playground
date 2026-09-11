# Exercise 08-02 – Provisioning Takes Minutes

Difficulty: Medium
Estimated Time: 25 minutes

## Skills

- long-running operations over HTTP (the `202 Accepted` pattern)
- polling with server hints
- deadlines, cancellation and transient errors while waiting

## Scenario

Creating a customer's cloud destination (storage account, database, permissions) takes the platform
30–90 seconds. The platform team moved `POST /provision` to the standard *asynchronous operation* pattern
used across the cloud platform:

```
POST /provision                      → 202 Accepted
                                        Operation-Location: /operations/op-123
                                        Retry-After: 5
                                        { "operationId": "op-123", "status": "Running" }

GET /operations/op-123               → 200 { "status": "Running" }                        Retry-After: 10
GET /operations/op-123               → 200 { "status": "Succeeded",
                                             "destination": { "destinationId": "dst-…", "region": "…" } }
                                     or 200 { "status": "Failed", "error": { "code": "…", "message": "…" } }
```

If the destination already exists, `POST /provision` answers `201 Created` with the destination straight away.
Provisioning that hasn't finished after **10 minutes** has failed; the platform cleans it up.

Since the change, the setup wizard shows "Destination ready" immediately, and the next step fails with
*"Destination '' not found"*.

## Your Task

Update `ProvisioningClient` to follow the new pattern.

## Constraints

- Keep the public API. `ProvisionAsync` returns when the destination is really ready.
- The constructor already accepts a `TimeProvider`. The tests use a fake clock.

## Acceptance Criteria

- Both the immediate (`201`) and the asynchronous (`202`) flows return the ready destination.
- Polling follows the server's `Retry-After` hints.
- A failed operation is reported with the platform's error and the operation ID.
- An operation still running after 10 minutes is reported as failed.
- A temporary error while polling does not abandon the operation.
- Cancellation stops the waiting immediately.

## How to Run

```bash
dotnet test exercises/08-reliability/08-02-provisioning-takes-minutes/tests
```

## When You're Done

All tests pass, and you can explain what happens to the destination if the user closes the wizard while provisioning is running.
