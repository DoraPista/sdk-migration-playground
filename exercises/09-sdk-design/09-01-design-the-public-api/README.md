# Exercise 09-01 – Design the SDK's Public API

Difficulty: Hard
Estimated Time: 30 minutes

## Skills

- public API design for a reusable library
- progress, cancellation and error contracts
- configuration and logging integration
- what to keep internal

## Scenario

The migration engine works. Its building blocks are in `src/MigrationKit.Engine/Internal/`, and every one of them is
`internal`. Nothing is public yet.

Product wants to ship it as **MigrationKit**, a DLL that desktop applications embed. The first two consumers are:

- the customer's **WPF** desktop app (.NET Framework 4.8 today, .NET 8 next year), and
- a new **.NET MAUI** app for site offices (Windows and Android).

A few partners will use it from their own tools too.

The requirements from product are in [`docs/requirements.md`](docs/requirements.md). Read them first.

## Your Task

Design the **public API** of MigrationKit.

Write the public types in `src/MigrationKit.Engine/PublicApi/`. Signatures and XML docs matter; bodies can be
`throw new NotImplementedException()`. Then write a short usage example showing how a WPF view model would
start a migration, show progress, cancel, and handle the result (`docs/usage.md`, or a `.cs` file).

Be ready to answer:

- Which types are public, and which stay internal? Why?
- How is progress exposed? On which thread does it arrive?
- How is cancellation exposed?
- How are errors represented? Which exceptions can callers get?
- How does the caller know that a migration has completed, and how it completed?
- Can several migrations run at once? How does the API make that safe?
- How does the SDK log? How does it fit the host's logging?
- How is the SDK configured? Where do credentials come from?
- How would a migration be resumed after the app restarts?

Ask whatever clarifying questions you think you need.

## Constraints

- The library targets **.NET Standard 2.0** (see the project file) because of the .NET Framework 4.8 host.
- No UI-framework types in the public API.

## How to Run

```bash
dotnet build exercises/09-sdk-design/09-01-design-the-public-api/src/MigrationKit.Engine
```

## What to Produce

A page of public types with the signatures of the four or five most important calls, and a usage snippet
showing what an app author writes. Say what you would leave out of v1.

## When You're Done

You have a public surface you'd be comfortable supporting for five years, and you can explain what you left out.
