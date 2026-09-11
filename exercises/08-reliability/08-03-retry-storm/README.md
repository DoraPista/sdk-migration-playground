# Exercise 08-03 – Retry Storm

Difficulty: Expert Discussion
Estimated Time: 20 minutes (plus optional experiments)

## Skills

- load dynamics during outages
- retry amplification
- backoff, jitter, budgets, circuit breaking
- reasoning with evidence

## Scenario

Last month the platform had a 60-second outage during a database fail-over. The outage itself was fine.
But after the database came back, the platform stayed unusable for **several more minutes**,
and on-call saw request rates far above anything the desktop fleet normally produces.

The platform team's post-mortem says: *"The desktop SDK made it worse."*

This folder contains a small, deterministic simulation of 300 desktop clients using the SDK's current
retry behaviour against a platform that can handle 200 requests per second. The retry behaviour is
in `src/ClientRetryPolicy.cs`. It's a faithful summary of what the SDK does today: the HTTP handler, the uploader and
the migration workflow each retry on their own.

## Your Task

1. Run the simulation and explain what you see.
2. Explain what problems retries can cause in an outage like this.
3. Propose changes to the SDK's retry behaviour, and to anything else you think matters.
4. *(Optional)* Change `ClientRetryPolicy` and re-run the simulation to test your ideas.

Ask whatever clarifying questions you think you need.

## Constraints

- You cannot change the server's capacity, and you cannot ask the platform team for a fix this week.
- The client fleet is ~2,000 desktops you cannot upgrade all at once.
- Uploads must still be retried: removing retries is not an answer.

## How to Run

```bash
dotnet run --project exercises/08-reliability/08-03-retry-storm/src
dotnet test exercises/08-reliability/08-03-retry-storm/tests
```

The simulation uses a fixed random seed. Every run with the same policy produces the same output.

## What to Produce

Numbers, not adjectives: the peak offered load before and after your change, whether the fleet recovers
once the outage ends, and how many requests each uploaded file costs. Then the policy you would ship.

## When You're Done

You can explain the graph, and why your changes would make the next post-mortem say something different.
