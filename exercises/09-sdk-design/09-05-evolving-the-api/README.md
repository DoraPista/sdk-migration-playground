# Exercise 09-05 – Shipping Version 2 Without Breaking Anyone

Difficulty: Expert Discussion
Estimated Time: 20 minutes

## Skills

- binary vs source compatibility
- semantic versioning in practice
- evolving a shipped SDK

## Scenario

MigrationKit 1.4 is in production. It is a DLL that ships **inside** customer applications:

- the WPF desktop app (built by us) updates the DLL with each release,
- two partner integrations reference the DLL and are compiled by the partners, sometimes years ago,
- one large customer **drops the new DLL into their installation folder without recompiling anything**.

The v1 public API is in [`src/MigrationKit.Api.V1/`](src/MigrationKit.Api.V1).

The team wants the changes listed in [`proposed-changes.md`](proposed-changes.md) for the next release.

## Your Task

For each proposed change, decide:

1. Is it **binary** breaking (an application compiled against 1.4 stops working when the new DLL is dropped in)?
2. Is it **source** breaking (a partner's code stops compiling when they upgrade)?
3. Is it **behaviourally** breaking (it compiles and runs, but does something different)?
4. What would you do instead, if anything?

Then answer: how would you ship this release? Version number, deprecation policy, and how you'd stop
someone from breaking compatibility by accident next time.

## Constraints

- The big customer drops the new DLL into an application they will not recompile.
- Partners compile against the SDK and will upgrade on their own schedule.
- "Do not change anything" is not an answer: the product needs these features.

## How to Run

Nothing to build for the discussion itself. Today's published surface is `src/MigrationKit.Api.V1`, and the
proposals are in `proposed-changes.md`. If you want to try one, the project compiles:

```bash
dotnet build exercises/09-sdk-design/09-05-evolving-the-api/src/MigrationKit.Api.V1
```

## What to Produce

A verdict per proposed change (binary / source / behavioural / safe, and what to do instead), plus the
release plan: version number, deprecation policy, and how you would stop this happening by accident again.

## When You're Done

You can defend each classification, and you have a plan the team could follow.
