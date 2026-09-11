# Exercise 16-01 – Design the Migration SDK

Difficulty: Expert Discussion
Estimated Time: 20 minutes (discussion)

## Skills

- API design
- separating a reusable library from the apps that use it
- naming the hard parts before writing code

## Scenario

Your team is about to build a migration SDK: one library that moves a customer's files and metadata from
their old system into the platform. Two apps will use it:

- a **WPF desktop app** the consultant runs at the customer's site, and
- a **MAUI app** the field team uses on a tablet to start a migration and watch it.

Later, a headless service will run migrations overnight.

Nothing is written yet. You are at the whiteboard.

## Your Task

Sketch the design. Cover:

1. The public API — the handful of types and calls an app author sees. What is the first line of code they write?
2. The workflow of a migration from "user picked a folder" to "done".
3. Authentication.
4. File transfer.
5. Retries and recovery after a crash or a lost connection.
6. Progress and cancellation.
7. Logging and what support sees when it fails.
8. What is persisted, where, and by whom.
9. What is in the SDK and what is left to the app — and why.

## Constraints

- One SDK, two very different UI frameworks. It must not reference either.
- A migration can take hours.
- The customer's network and the platform are both unreliable.

## How to Run

Nothing to build. This is a discussion: use a whiteboard, a scratch file or paper. The repository is
here for reference — point at real code when it helps your argument.

## What to Produce

A diagram or a list of types, and the signatures of the three or four most important calls.
Pseudo-code is fine. Say what you would leave out of v1.

## When You're Done

You can defend the shape of the API, say what happens when the laptop's lid closes mid-migration,
and name the parts you are least sure about.
