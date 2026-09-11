# Exercise 16-04 – Many Migrations at Once

Difficulty: Expert Discussion
Estimated Time: 20 minutes (discussion)

## Skills

- resource management
- shared state and isolation
- capacity and fairness

## Scenario

Until now one consultant runs one migration on one laptop. Two things are changing at once:

- consultants want to run **three or four migrations in parallel** from the same machine (different
  customers, different destinations), and
- the company wants an **unattended runner** on a server that picks migrations off a queue and runs
  perhaps twenty at a time.

The SDK was written assuming one migration per process.

## Your Task

1. What in a one-migration-per-process design breaks when there are twenty? Be specific — name the state.
2. How are the migrations isolated from each other, and what is deliberately shared?
3. How do you bound the resources they compete for (connections, threads, memory, disk, the platform's rate limit)?
4. One customer's migration is 100 GB and another's is 200 MB. How do you stop the big one starving the small one?
5. What does an operator need to see and be able to do?

## Constraints

- The platform rate-limits per **tenant**, not per process.
- The server has 8 cores and 16 GB of RAM.
- One migration failing must not affect the others.

## How to Run

Nothing to build. This is a discussion: use a whiteboard, a scratch file or paper. The repository is
here for reference — point at real code when it helps your argument.

## What to Produce

The ownership model (what object owns what, and what is a singleton), plus the limits you would set and
how you would choose the numbers.

## When You're Done

You can say what you would measure in production to know whether the limits are right.
