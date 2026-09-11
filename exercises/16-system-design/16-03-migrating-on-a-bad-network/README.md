# Exercise 16-03 – Migrating on a Bad Network

Difficulty: Expert Discussion
Estimated Time: 20 minutes (discussion)

## Skills

- designing for failure rather than around it
- retries, idempotency, and resumption
- honest progress reporting

## Scenario

A construction customer's site office has a 4G router shared by thirty people. Measured over a working day:

- throughput swings between 200 KB/s and 6 MB/s,
- the connection drops completely for 30–90 seconds several times an hour,
- one request in fifty returns a 502 from something between the laptop and the platform,
- at 17:00 the whole site VPN restarts.

The consultant starts a 20 GB migration on Monday and expects it to be finished by Wednesday.
They close the laptop lid each evening.

## Your Task

Design the migration for this network.

1. What does the client do on each kind of failure — timeout, 502, connection reset, DNS failure, 429, 401?
2. How does it avoid sending the same data twice, and how does it know what actually arrived?
3. What survives closing the lid, killing the app, and rebooting the laptop?
4. What does the consultant see on screen while all of this is happening?
5. When do you stop trying, and what do you leave behind for the next run?

## Constraints

- You cannot change the customer's network, and you cannot ask them to leave a laptop plugged in overnight.
- The platform rate-limits and occasionally returns 503 with `Retry-After`.
- The customer will ask "is it stuck?" at least twice a day.

## How to Run

Nothing to build. This is a discussion: use a whiteboard, a scratch file or paper. The repository is
here for reference — point at real code when it helps your argument.

## What to Produce

A description of the retry/resume model — the states, what is written to disk and when, and the rules
for giving up. A diagram helps.

## When You're Done

You can explain how the system knows a chunk arrived even when the response was lost, and what stops a
retry storm when the VPN comes back.
