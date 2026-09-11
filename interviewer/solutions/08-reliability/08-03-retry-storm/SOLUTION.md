# 08-03 Retry Storm – Interviewer Notes

**Type:** expert discussion with evidence · **Time:** 20 min · **Solution code:** `code/`
**Maps to:** special scenario **J (Retry Storm)**

## Hints

1. "Run the simulation and watch the offered load, not the failure count. What are the clients doing at t = 60 s?"
2. "There are three nested layers of retry. Multiply their attempt counts. How many requests does one user action become?"
3. "Retry at one level only, add full jitter, honour Retry-After, and add a budget or a circuit breaker so a dead dependency stops being hammered."

## The numbers

Run `dotnet run --project exercises/08-reliability/08-03-retry-storm/src`. With the shipped policy:

```
Peak offered load         : 810 req/s (4.1x capacity)
Recovery after the outage : never (within the simulation)
Total requests            : 181,697
Files uploaded / failed   : 32,179 / 4,600
Requests per uploaded file: 5.65
```

With the improved policy in `code/src/ClientRetryPolicy.cs`:

```
Peak offered load         : 281 req/s (1.4x capacity)
Recovery after the outage : 0 s
Total requests            : 53,814
Files uploaded / failed   : 50,991 / 303
Requests per uploaded file: 1.06
```

Same outage, same fleet. The difference is entirely client behaviour: **58% more files migrated with 70% fewer requests**.

## What the candidate should see in the graph

1. **Amplification.** Three nested retry layers (handler × uploader × workflow) = up to 27 requests per file.
   During the outage, offered load goes from 150 req/s to ~800 req/s: the fleet hits hardest exactly when the platform is weakest.
2. **Synchronisation.** A fixed 1-second delay with no jitter means everyone who failed together retries together: a thundering herd every second.
3. **Metastable failure.** After the outage ends, the platform *stays* overloaded. Retries alone are enough to keep demand above capacity, and
   an overloaded platform wastes capacity on requests it rejects, so goodput falls further. The system never leaves the bad state on its own,
   even though the original trigger is long gone. (This is the mechanism behind many "we had to restart everything" incidents.)
4. **It's worse for users, not just the platform.** More requests *and* fewer migrated files, because retry budgets are burned while the platform is down.

## Problems retries cause in an outage (the discussion)

- Load amplification and self-sustaining overload (above).
- Retry budget exhausted while recovery is impossible, so work fails that would have succeeded 30 seconds later.
- Queues and connections held open; connection-pool and port exhaustion on the client.
- Non-idempotent retries creating duplicates (07-03).
- Long backoffs hiding real errors from users, with no feedback.
- Cost: in a cloud platform, a 4× request rate is also a 4× bill for the rejected traffic.

## Mitigations to expect

| Mitigation | Notes |
|---|---|
| **Retry at one layer only** | The biggest single win. Decide where retries live (usually the transport/uploader) and make the others pass failures up |
| **Exponential backoff with jitter** | De-synchronises the fleet. "Full jitter" is the usual default |
| **Honour `Retry-After`** | Lets the platform pace its own recovery |
| **Cap attempts and total time (retry budget)** | Fail the file, resume the migration later (10-03) |
| **Circuit breaker / adaptive concurrency** | After N consecutive failures, stop trying for a while; probe with one request. The reference policy does a simple version of this |
| **Server-side load shedding** | Reject early and cheaply (a cheap 429 beats an expensive 503), rate limits per client, admission control |
| **Queue-based work** | For big fleets, uploads through a queue with server-controlled pacing (16-04) |
| **Stagger restarts** | After a platform restart, clients should not all reconnect at once |

## Follow-up questions

- "Where in *our* SDK are the three retry layers?" (`RetryingHttpHandler`, `FileUploader`, `MigrationWorkflow`: exercise 08-01 is the single layer.)
- "Would a circuit breaker have helped here, and what would it protect?" (The platform, and the client's own resources; it needs a probe strategy to avoid staying open.)
- "What would you add to telemetry to catch this earlier?" (Requests per successful operation; retry rate; client-side rejection rate.)
- "Is there anything the platform could do that the client cannot?" (Yes: shed load cheaply, return `Retry-After`, throttle per tenant, and cap the damage regardless of client behaviour. Never trust clients to be polite.)
- "What if the fleet were 10,000 clients instead of 300?" (Change `SimulationSettings.Clients` and re-run.)

## Scoring notes

| Level | Indicators |
|---|---|
| Weak | "Add a longer delay"; doesn't see the multi-layer amplification; reads the graph as "the outage was long" |
| Solid mid-level | Identifies amplification and synchronisation; proposes backoff + jitter + one retry layer + caps; can explain the graph |
| Strong | Names the metastable state and why it persists; uses `Retry-After`; adds a budget/circuit breaker; validates by re-running the simulation |
| Senior | Discusses server-side protections, telemetry, fleet-wide coordination, cost, and the trade-off between user-visible failures and platform health |
