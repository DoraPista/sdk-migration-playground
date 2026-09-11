# 16-04 Many Migrations at Once – Discussion Notes

**Type:** system design · **Time:** 20 min

## Hints

1. "Name the state that exists exactly once per process today."
2. "Two migrations for two customers, one HttpClient with a default Authorization header. What goes out on the wire?"
3. "Per-run ownership of identity, checkpoint and limits; a shared handler and a per-tenant rate limiter; bounded concurrency; and a scheduling rule so the 100 GB job does not starve the 200 MB one."

## 1. What a one-migration-per-process design gets wrong

Push for **specific state**, not "it wouldn't be thread-safe":

- `static` anything: the cached token (06-01, 15-01), a current-migration field, a logger scope, a
  `HttpClient` configured with one tenant's base address and auth header.
- A single `HttpClient` whose `DefaultRequestHeaders.Authorization` is set per call — two migrations, two
  tenants, one header: requests go out with the wrong customer's token. This is the scariest one and it is a
  **data-leak class bug**, not a performance bug.
- Checkpoint files keyed by a fixed name, or a temp directory shared between runs.
- Global progress/counters, `Console` output interleaved, a log file opened exclusively.
- `Environment.CurrentDirectory`, `Thread.CurrentPrincipal`, `CultureInfo` set globally.
- Concurrency limits that are per-migration, so twenty migrations mean twenty times the limit.

## 2. Isolation and deliberate sharing

- **Per migration:** identity/credential, destination, checkpoint store, progress, cancellation source,
  retry budget and circuit breaker, log scope with the migration id.
- **Shared on purpose:** the `SocketsHttpHandler` connection pool (one handler, many clients — sharing
  sockets is good; sharing headers is not), the thread pool, the file-system, a per-tenant rate limiter.
- The clean shape: everything per-migration lives on a `MigrationRun` object; the process owns exactly one
  `MigrationHost`/`SdkRuntime` holding the shared bits. Nothing static and mutable.

## 3. Bounding the resources

Name the resource, the limit, and how the number is chosen:

| Resource | Limit | How to choose |
|---|---|---|
| Concurrent migrations | 4 on a laptop, ~20 on the server, both configurable | Memory per migration × N < budget |
| Concurrent uploads per migration | 2–8 | Measured: increase until throughput stops improving or 429s appear |
| Total concurrent requests per **tenant** | A shared limiter, because the platform limits per tenant | The platform's documented limit minus headroom |
| Memory | Bounded buffers/`ArrayPool`, streaming, no whole files | Chunk size × concurrency × migrations |
| Threads | No blocking calls; bounded parallelism, never `Task.WhenAll` over everything | |
| Disk | Checkpoints and logs per migration, with rotation | |

The subtle point: a **per-tenant** limiter has to be shared across migrations of the same tenant but not
across tenants. Twenty migrations for one customer must together respect one budget.

## 4. Fairness

- Without scheduling, the 100 GB migration takes every slot for a day. Options:
  round-robin work between active migrations; weight by remaining bytes; reserve slots for small jobs
  ("one lane for anything under 1 GB"); or simply run big migrations in a separate queue/worker pool.
- Ask what the business wants: shortest-job-first makes most customers happy fastest, FIFO is fairest,
  deadline-based matches contracts. A candidate who asks *which* is the right instinct.
- Starvation must be bounded: even with priorities, every migration gets some progress or an explicit "queued".

## 5. What an operator needs

- Per-migration: state, throughput, errors, retries, last success, ETA, and who it belongs to.
- Actions: pause, resume, cancel, change concurrency, re-run failed files.
- Process-level: total throughput, 429 rate, queue depth, memory; alerts on "no progress for X".
- Structured logs with the migration id, and **no customer credentials anywhere** (15-01).
- The ability to kill one migration without touching the others: that is a design requirement, and it is
  what makes `CancellationTokenSource` per run mandatory.

## Follow-ups

- "Two migrations for the same tenant. Where do they contend?" (Rate limit, token, quota.)
- "One migration deadlocks. What stops the rest going down with it?" (No shared blocking, per-run timeouts, supervision.)
- "The server has 16 GB. How many can you run?" (Make them do the arithmetic out loud.)
- "Would you use one process per migration instead?" (Legitimate: isolation for free, cheap failure containment, at the cost of memory and shared limits. A good candidate weighs it rather than dismissing it.)

## Level indicators

| Level | Indicators |
|---|---|
| Weak | "Use locks"; no named state; per-migration limits with no global view |
| Solid mid-level | Removes statics, per-run ownership, bounded concurrency, isolation of failures |
| Strong | Spots the per-tenant limit, the shared-handler-vs-shared-headers distinction, and the fairness problem |
| Senior | Treats it as capacity planning and operations: measurable limits, scheduling policy chosen with the business, process-per-migration considered honestly |
