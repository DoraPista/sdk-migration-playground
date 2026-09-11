# 16-02 From 100 MB to 100 GB – Discussion Notes

**Type:** system design · **Time:** 20 min

## Hints

1. "Which *assumption* breaks first? Not which code is slowest."
2. "Do the arithmetic out loud: 1.2 million files at one extra round trip each, and 100 GB at the site's real bandwidth."
3. "Streaming and resumability are feasibility; batching and concurrency are optimisation. Order the list that way and say which you would do first."

The question is not "how would you build it" but **"which assumption breaks first"**. A candidate who
starts ordering the failures without being asked is already doing well.

## What breaks, roughly in order

1. **Anything that holds a whole file in memory.** `File.ReadAllBytes`, `ReadAsStringAsync`, a `byte[]` for
   hashing. A 40 GB file is not going in a `byte[]` at all (2 GB array limit), never mind in RAM. → streaming
   end to end, chunked upload, hash as you stream (07-01).
2. **Anything that holds the whole *list* in memory or is O(n²) in the file count.** 1.2 million files:
   the planning step (14-01), a `List<PlanItem>` serialized repeatedly, LINQ scans per file, a `DataGrid`
   bound to every row. → streaming enumeration, batching, `HashSet`/dictionary lookups, virtualized UI.
3. **Per-file round trips.** At 1.2 million files, one extra request per file at 50 ms is 17 hours of
   latency alone. → batch metadata, pipeline requests, bounded parallelism, and ask the platform team for a
   bulk endpoint (this is the API change worth requesting early, because it takes weeks).
4. **Time-based assumptions.** Tokens expire mid-run (06-01), the job outlives an HTTP timeout, an
   `HttpClient.Timeout` of 100 s applied to a 40 GB PUT, the laptop sleeps. → refresh tokens, per-chunk
   timeouts not per-file, resumable sessions.
5. **All-or-nothing semantics.** A single failure at 96 % that restarts everything is unusable. →
   checkpoint per chunk, resume from the server's view of what arrived (07-02, 10-03).
6. **Progress and ETA.** A percentage computed from file counts is meaningless when one file is 40 GB. →
   weight by bytes; report two numbers.
7. **Disk on the laptop.** Temp copies, logs and checkpoints for 1.2 million files add up; a verbose log line
   per file is gigabytes of log.
8. **The UI.** Any list of 1.2 million rows, any per-file UI update (11-02, 14-02).
9. **Verification.** Re-hashing 100 GB to prove it arrived takes hours. → hash per chunk during upload,
   have the server return what it computed, and verify incrementally.

## What you cannot fix in eight weeks — and what you say instead

Good answers separate engineering from expectation management:

- You will not make 100 GB go over a site 4G link in an afternoon. Compute the floor: at a *sustained*
  20 Mbit/s, 100 GB is ~11 hours of pure transfer. State that number early and design around it
  (overnight runs, restartability, a wired connection, or shipping a disk).
- You may not get a bulk metadata endpoint in time. Plan around what exists.
- Delta/incremental migration and parallel machines are v2.

## Measuring without two-day runs

- Generate a synthetic export (the repo's `large` dataset and `shared/MockData/tools` are exactly this).
- Make the mock server slow/faulty on demand (`shared/MockServer` fault injection) rather than waiting for reality.
- Measure phases separately: planning throughput (files/s), transfer throughput (MB/s), overhead per file.
- Extrapolate honestly: 1 % of the files, times 100, plus the parts that are non-linear — and *say* which
  parts are non-linear (memory, the UI list, anything O(n²)).
- Put a guard test in CI (14-01 does: call count and allocation).

## Follow-ups

- "Which single change buys the most?" (Usually: stop loading files into memory and make the run resumable —
  everything else is optimisation, those two are feasibility.)
- "The 40 GB file fails at 38 GB. What happens?" (Resume at the chunk, not the file.)
- "Planning takes two days. Where is the time?" (Drive them to measure, not guess — 14-01.)
- "Would you parallelise across machines?" (Interesting, but tenant rate limits and ordering constraints bite first.)

## Level indicators

| Level | Indicators |
|---|---|
| Weak | Jumps to "use more threads"; no ordering; no numbers |
| Solid mid-level | Memory, per-file round trips, resumability and progress, in a sensible order; proposes measuring |
| Strong | Does the arithmetic (hours of latency, hours of transfer); separates feasibility from optimisation; plans the API request early |
| Senior | Manages the customer conversation as part of the design; picks what to cut; builds the measurement harness before the fixes |
