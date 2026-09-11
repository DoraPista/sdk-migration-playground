# Exercise 16-02 – From 100 MB to 100 GB

Difficulty: Expert Discussion
Estimated Time: 20 minutes (discussion)

## Skills

- reasoning about scale
- knowing which assumptions break first
- prioritising work under a deadline

## Scenario

The SDK you have works. Every customer so far has migrated between 50 MB and 2 GB, a few thousand files,
in under an hour.

Sales have signed a customer with **100 GB across 1.2 million files**, including some single files over
40 GB (point clouds and survey scans). The migration is in eight weeks. The current code, pointed at their
export, has been running for two days and has not finished the planning step.

## Your Task

1. What breaks first, and why? Order the list.
2. For each, what changes — in the SDK, in the protocol, in the UI, in the customer's expectations?
3. What can you *not* fix in eight weeks, and what would you tell the customer instead?
4. How do you find out whether your changes worked, without waiting two days each time?

## Constraints

- The platform API is what it is: you can request changes, but each takes weeks.
- The consultant runs this on a laptop at the customer's office.
- The migration must be resumable. Nobody will start 100 GB again from zero.

## How to Run

Nothing to build. This is a discussion: use a whiteboard, a scratch file or paper. The repository is
here for reference — point at real code when it helps your argument.

## What to Produce

An ordered list of the failure points with the change each one needs, and a one-paragraph plan for the
eight weeks.

## When You're Done

You can say which single change buys the most, and what you would measure to prove it.
