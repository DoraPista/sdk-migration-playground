# Exercise 05-02 – The Server Upgrade That Broke Every Desktop

Difficulty: Medium
Estimated Time: 20 minutes

## Skills

- JSON contract evolution
- API versioning
- tolerant readers
- polling

## Scenario

Monday morning the platform team rolled out a new release of the Migration Platform API. By 10:00, every
desktop app watching an active migration showed:

> ⚠ Unexpected error: The JSON value could not be converted to MigrationKit.Status.MigrationState.

and stopped updating.

The platform team's position (from their API guidelines, which they say were published last year):

- Clients **must** send the API version they were built against in an `api-version` header.
  Requests without it are served by the **latest** version.
- Within a version, the platform may **add** response properties at any time. That's not a breaking change.
- New enum values only appear in new API versions. (Although the March hotfix accidentally shipped a
  `Paused` state in version 1. It's being fixed, but some servers still send it.)

Our SDK's status client was built against version `1`.

## Your Task

Make `MigrationStatusClient` robust against this kind of server change.

## Constraints

- Keep the public API of `MigrationStatusClient`.
- `WaitForCompletionAsync` must keep working while the server reports states it doesn't know.

## Acceptance Criteria

- The client talks to the version it was built against.
- New response properties don't break the client.
- A state value the client doesn't recognise doesn't break the client, and isn't mistaken for completion.

## How to Run

```bash
dotnet test exercises/05-http-api/05-02-server-upgrade-breaks-client/tests
```

## When You're Done

All tests pass, and you can say what the SDK should promise its own consumers about enum values.
