# Exercise 10-04 – The Upload That Might Have Worked

Difficulty: Expert Discussion
Estimated Time: 20 minutes

## Skills

- reasoning about distributed failure
- exactly-once vs at-least-once
- designing recovery you can defend

## Scenario

```
  Desktop app                                   Migration platform
      │                                                │
      │  POST /migrations/mig-00102/files  (18 MB)     │
      │───────────────────────────────────────────────>│
      │                                                │  stores the file
      │                                                │  writes the database row
      │            201 Created { fileId: "file-01234" }│
      │        X──────────────  connection reset ──────│
      │                                                │
      │  HttpRequestException                          │
      ▼                                                ▼
```

The client does not know whether the platform stored the file.

This happens for real: a hotel Wi-Fi drop, a laptop switching from Wi-Fi to LTE, a load balancer recycling a
node, or the platform's gateway timing out a slow upload that the backend finished anyway.

You can see it happen. `src/` is a small program that runs the local mock platform, loses one response on
purpose, and prints what each side believes:

```bash
dotnet run --project exercises/10-migration/10-04-lost-upload-response/src
```

## Your Task

Discuss, with the interviewer:

1. What are the possible states of the world when the client gets that exception? (Be exhaustive.)
2. What should the SDK do next, and what does the user see while that happens?
3. What would you need from the platform's API to do it well? What if the platform team says
   "we can't change the API this quarter"?
4. How do your answers change when the file is 18 MB, 18 GB, or when there are 500 of them?
5. Can this be made *exactly once*? If not, what do you promise the customer instead?
6. How would you test your answer?

Ask whatever clarifying questions you think you need.

## Constraints

- You cannot change the network, and the failure will happen again tomorrow.
- The platform team will add an endpoint if you ask for the right one - say what it should return.
- Uploading the same file twice is not acceptable to the customer.

## How to Run

A small program reproduces the ambiguous result end to end against the mock platform:

```bash
dotnet run --project exercises/10-migration/10-04-lost-upload-response/src
```

## What to Produce

The list of states the world could be in, what the client does about each, and the endpoint or header you
would ask the platform team for.

## When You're Done

You can defend a recovery strategy, including what it costs and what it still cannot guarantee.
