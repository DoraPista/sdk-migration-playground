# Exercise 16-05 – Evolving the SDK Without Breaking Desktop Clients

Difficulty: Expert Discussion
Estimated Time: 20 minutes (discussion)

## Skills

- versioning and compatibility
- knowing what "breaking" means
- shipping to machines you do not control

## Scenario

The SDK is v1.4 and is used by:

- the WPF desktop app, shipped to ~200 consultant laptops via an MSI (some are still on v1.1 and will not
  be updated until the customer's IT department allows it),
- the MAUI app, updated through the stores,
- two customer integrations that reference the DLL directly — one of them still builds against
  **.NET Framework 4.8**,
- an internal overnight runner.

You need to add resumable uploads, change how progress is reported, and rename two badly named methods.
The platform API is also moving to `api-version=2026-03-01`.

## Your Task

1. Which of the changes you want to make are breaking? Distinguish binary, source and behavioural breaks.
2. How do you ship them without stranding the v1.1 laptops?
3. What is your versioning and support story — numbers, branches, how long you support what?
4. How does the SDK deal with a platform API version that changes underneath it?
5. What would you have done differently in v1.0 to make this easier?

## Constraints

- You cannot force an upgrade on the consultant laptops.
- The .NET Framework 4.8 integration must keep working.
- Two versions of the SDK may end up in the same customer's environment.

## How to Run

Nothing to build. This is a discussion: use a whiteboard, a scratch file or paper. The repository is
here for reference — point at real code when it helps your argument.

## What to Produce

A policy you could put in the SDK's README: what changes are allowed in which release, and how deprecation
is communicated. Plus the concrete plan for the four changes above.

## When You're Done

You can give an example of a change that compiles fine, does not break the ABI, and still breaks a customer.
