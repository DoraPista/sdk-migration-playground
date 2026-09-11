# Exercise 06-02 – Secrets in the Support Logs

Difficulty: Medium
Estimated Time: 20 minutes

## Skills

- secure logging
- structured logging pitfalls
- exception messages as a data leak

## Scenario

To help customers, support asks them to enable **Verbose logging** in the desktop app and send the log file.
A security review of 50 real support tickets found:

- client secrets for 7 customers,
- live access tokens,
- upload URLs that still worked (they contain a SAS signature that is valid for 24 hours),

all pasted into the ticketing system, which half the company can read.

The SDK must never write credentials, tokens or signatures to logs, **at any log level**, including in exception
messages (hosts log exceptions too). Support still needs useful logs: what was called, what came back, for which client.

## Your Task

Find every leak and fix it.

## Constraints

- Keep the public API.
- Don't remove logging that support needs: request method and path, status codes, client ID, timings.

## Acceptance Criteria

- Client secrets, access tokens and SAS signatures appear nowhere in log output or exception messages, at any level.
- The logs still show methods, paths, status codes and the client ID.

## How to Run

```bash
dotnet test exercises/06-authentication/06-02-secrets-in-support-logs/tests
```

## When You're Done

All tests pass, and you can suggest how to stop this class of leak from coming back.
