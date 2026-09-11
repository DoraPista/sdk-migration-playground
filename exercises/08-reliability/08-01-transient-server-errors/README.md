# Exercise 08-01 – The Platform Has Bad Minutes

Difficulty: Medium
Estimated Time: 30 minutes

## Skills

- retry policies
- transient vs permanent failures
- backoff, jitter and server hints
- cancellation and time in tests

## Scenario

During the platform's deployments, and whenever a node is recycled, requests fail for a few seconds.
Customers report that migrations fail with *"Metadata upload failed with 503"* although, as they put it,
"the service works again if you just click Retry".

`MetadataUploader` doesn't retry at all. Your team lead wants it to be resilient, but has seen retry code
go wrong before:

> "Don't hammer the platform when it's already struggling. Don't retry things that can never succeed,
> and don't make the user wait forever either. If the platform tells us when to come back, listen to it.
> Authentication has its own component (see 06-01), so leave 401 alone here."

The platform may answer with `429 Too Many Requests` and a `Retry-After` header (in seconds).

Uploads carry an `Idempotency-Key`, so the platform will not store a document twice if a request is repeated.

## Your Task

Make `MetadataUploader` resilient to transient failures without retrying inappropriately.

Ask whatever clarifying questions you think you need.

## Constraints

- Keep the public API. The constructor already accepts a `TimeProvider`. The tests use a fake clock and must never wait real seconds.
- Give up if the platform is still failing after roughly two minutes. The migration can be resumed later.

## Acceptance Criteria

- Transient failures (server errors, throttling, connection problems) are recovered from.
- Requests that can never succeed fail immediately, with the status code.
- `Retry-After` is respected.
- There is a pause between attempts, and the total number of attempts and the total waiting time are bounded.
- Cancellation during a pause stops the upload immediately.

## How to Run

```bash
dotnet test exercises/08-reliability/08-01-transient-server-errors/tests
```

## When You're Done

All tests pass, and you can justify every number in your policy.
