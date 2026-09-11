# 15-01 Review This PR: Bulk Upload – Interviewer Notes

**Type:** code review · **Time:** 25 min · **No code to fix** — this measures reasoning and communication.

## Hints

1. "Decide what you would say if the author were about to merge in five minutes."
2. "Look at what is static, what is written to the console, what happens when two uploads finish at the same moment, and what that retry loop retries."
3. Point at one line - the Console.WriteLine with the user and password - and ask what else in the file has the same character.

There are roughly twenty things to find. Nobody finds them all in ten minutes, and a candidate who tries to
list everything without ranking is showing you something too. What matters is **what they lead with**, whether
they explain the customer-visible effect, and how they talk to the author.

The PR description contains three confident, wrong claims (`using` on `HttpClient`, "parallel made it fast",
"hard to test because of the HTTP calls"). Watch whether the candidate accepts them.

## Blocking findings

| # | Line | Finding | Why it matters |
|---|---|---|---|
| 1 | `UploadProject` signature | **`async void`** on a public API | The caller cannot await it or know when it finished; any exception escapes to the synchronization context and kills the app. The demo will show "Done" before anything has uploaded |
| 2 | `Console.WriteLine(... user + "/" + password)` and `"Signed in, token = " + _token` | **Credentials and the bearer token are logged** | A support bundle from one customer now contains another customer's password. This is the one finding that would fail a security review on its own |
| 3 | `new HttpClient()` per file, in a `using` | Socket exhaustion: each disposal leaves a `TIME_WAIT` socket for ~4 minutes | The author explicitly defends this in the description. With 5,000 files the app runs out of ephemeral ports and starts failing with `SocketException`. Use one shared client / `IHttpClientFactory` |
| 4 | `tasks.Add(UploadOne(...))` for every file | **Unbounded concurrency** | 5,000 simultaneous requests: the platform rate-limits or the connection collapses. Bound it (`Parallel.ForEachAsync` with `MaxDegreeOfParallelism`, or a `SemaphoreSlim`) |
| 5 | `Results.Add(...)` / `Uploaded.Add(...)` from parallel tasks | **`List<T>` and `HashSet<T>` are not thread-safe** | Lost entries, or a corrupted internal state that throws in an unrelated place later. The summary the customer sees is wrong |
| 6 | `throw ex;` | Resets the stack trace (the build even warns, CA2200) — and it throws *out of one task inside `Task.WhenAll`* | The first failure is the only one reported, the rest are swallowed, and the file's result is never recorded |
| 7 | `private static string _token` | Static mutable state, set by a check-then-act race, never refreshed | Two migrations (or two tests) in one process share it. When it expires everything 401s, and the retry loop retries the 401 five times |
| 8 | No `CancellationToken` anywhere | The user cannot stop a migration; closing the window leaves the work running | |

## Worth raising, not necessarily blocking

| # | Finding | Note |
|---|---|---|
| 9 | `File.ReadAllBytes` | Whole file in memory. Fine for 2 MB drawings, fatal for the 4 GB point-cloud files this product has (see 07-01) |
| 10 | `Thread.Sleep(1000)` in an `async` method | Blocks a thread-pool thread; with the unbounded parallelism above it starves the pool. Fixed delay, no jitter, no `Retry-After` |
| 11 | Retries **every** non-success status | 400, 401, 403 and 404 will never succeed; retrying them five times wastes 5 s per file and can lock the account |
| 12 | No idempotency key | A retried upload that actually succeeded the first time duplicates the file on the platform |
| 13 | URL built by concatenation; the file name is not escaped | `Site plan #3 & final.pdf` breaks the query string. `Uri.EscapeDataString` |
| 14 | Login body built by string concatenation | A password containing `"` or `\` produces invalid JSON — and this is JSON injection. Serialize an object |
| 15 | `DateTime.Now` | Local time sent to a server; a migration run at 01:30 on a DST boundary is ambiguous. `DateTimeOffset.UtcNow`, round-trip format |
| 16 | `Console.WriteLine` as the logging story | An SDK should take `ILogger`; the desktop app cannot route this anywhere |
| 17 | `Summary()` uses integer division | Anything under 1 MB reports "0 MB" |
| 18 | No progress reporting | A 10,000-file project shows nothing for twenty minutes |
| 19 | `UploadResult` public mutable fields; `BulkUploader` public mutable fields (`BaseUrl`, `MaxRetries`, `Results`) | Anything can mutate the results; the hard-coded internal URL will ship |
| 20 | `SkipAlreadyUploaded` | Uses in-process state, so "resume" forgets everything on restart, and `UploadProject` never calls it. Dead, misleading API |
| 21 | "Hard to test because of the HTTP calls" | Not true: inject `HttpMessageHandler` or an interface. What makes it untestable is the statics, `Console`, and `Directory.GetFiles` — the design, not HTTP |
| 22 | `<Nullable>disable</Nullable>` in the new project | The rest of the repo has it on; this silences the warnings that would have caught `_token` being null |

## What distinguishes the levels

**Junior** — finds the obvious ones: no tests, `Console.WriteLine`, magic strings, maybe `async void` because
they have been told it is bad. Describes rules ("you shouldn't use static") rather than consequences.

**Mid-level** — finds `async void`, the `HttpClient` usage, the shared-list race, the swallowed/rethrown
exception, the logged credentials, and the missing cancellation. Explains what the customer would see.
Separates blocking from non-blocking. Asks the author questions rather than pronouncing.

**Senior** — everything above, plus:
- notices that **unbounded parallelism against a rate-limited API is not "fast", it is a retry storm** (08-03),
- asks about idempotency and what happens when the same batch is retried after a crash (07-03, 10-03),
- talks about how this API will evolve (it is a class with public fields; every one of them is now a contract),
- asks what the *demo on Thursday* actually needs and proposes a smaller, safe subset rather than blocking outright,
- raises operational questions: what does support see when this fails for one customer at 2 am?

## Facilitating

- If they go line-by-line without prioritising: *"You have five minutes with the author before they merge. What do you say?"*
- If they miss the security issue: *"This runs at a customer site and support asks for the log file. Anything you would change first?"*
- If they accept the `using` claim: *"Walk me through what `Dispose` on `HttpClient` does to the socket."*
- If they only criticise: *"What is good in this PR?"* (Answer: it is readable, it has a result type, it retries at all, and the author says exactly what they did and did not do — worth saying out loud.)

## Scoring notes

| Level | Indicators |
|---|---|
| Weak | Style comments; misses every concurrency issue; no prioritisation; tone that would annoy the author |
| Solid mid-level | 6–10 real findings including at least two of {async void, HttpClient, shared-list race, logged secrets}; ranks them; phrases comments constructively |
| Strong | Also cancellation, retry classification, memory, idempotency; challenges the PR description's claims with evidence |
| Senior | Architectural and operational framing, the demo trade-off, API evolution, and what they would want to see tested before merge |
