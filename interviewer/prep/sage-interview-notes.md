# Sage Interview Notes

Notes for `prep/sage-interview-questions.md`. For each question: what the interviewer is probing, what a strong
answer covers, and where to practise. Read them **after** answering out loud. These are the points a strong answer
touches, not a script; interviewers reward reasoning and trade-offs more than completeness.

## A. The SDK itself

**A1. Structure for .NET Framework 4.7.2 plus a modern app.** *Probing:* .NET Standard, layering, keeping UI out
of the SDK.
- Ask first: which hosts, which .NET versions, one DLL or a NuGet package, who owns the hosts.
- A core SDK targeting `netstandard2.0`, or multi-targeting `netstandard2.0;net10.0`, with no references to
  WPF, WinForms or MAUI.
- Thin optional adapters for UI concerns (dispatcher-friendly progress), or none at all: plain `IProgress<T>` works in both.
- Minimal dependencies, polyfill packages for newer APIs, and tests run on a .NET Framework host as well as modern .NET.
- *Strong:* says why not modern-.NET-only (the host is Framework) and what multi-targeting costs.
- Practise: 09-03, 09-04, quick-fire H1–H3.

**A2. The public API.** *Probing:* designing for the person calling your code.
- Starts from the call site, a few lines a WPF developer would write.
- An instance client built from options, not a static. Something like
  `StartAsync(request, IProgress<MigrationProgress>, CancellationToken)`, returning a handle or result with an id.
- Progress reported as immutable snapshots, with the thread it arrives on documented. Cancellation built in from
  the start. Failure as a result that lists per-item problems.
- Resuming by migration id. Whether two migrations can run at once. What is deliberately *not* in version 1.
- Practise: 09-01 (compare with the reference in `interviewer/solutions`).

**A3. Exceptions or results.** *Probing:* modelling failure.
- Throw for programming errors (bad arguments), for environments that cannot work (the host has no network stack),
  and for cancellation (`OperationCanceledException`).
- Return results for *expected* outcomes: "completed, 3 files failed validation", "needs attention".
- A small typed exception hierarchy with error codes, never swallowing, always keeping the inner exception.
- Practise: 01-03, 02-02, 03-03.

**A4. The version conflict.** *Probing:* whether they understand running as a DLL inside a .NET Framework app.
- .NET Framework loads one version of an assembly per AppDomain; the host's `app.config` binding redirects
  decide which. Without a matching redirect you get `FileLoadException`; redirected to an older version, you get
  `MissingMethodException` when a newer method is called.
- The SDK cannot edit the host's configuration. Options: depend on the lowest version that works; avoid the
  dependency; embed a private copy (licence permitting); or isolate it (heavy).
- *Strong:* **the host is Sage's own product**, so unlike a third-party SDK, the teams can agree a supported
  dependency set and the redirects together. Say that coordination is part of the design.
- Practise: 09-04, quick-fire H5.

**A5. Versioning across independent release cycles.** *Probing:* compatibility thinking.
- SemVer: only additions in minor versions; `[Obsolete]` with a message and a removal version.
- Compatibility tooling (`PublicApiAnalyzers`, package validation against the last release).
- The server keeps supporting old API versions for a long time; the client is a tolerant reader.
- The server can tell an SDK that is too old to upgrade, with a clear message, rather than failing oddly.
- Practise: 09-05, 16-05, 05-02.

**A6. Logging.** *Probing:* libraries that behave well inside a host.
- Accept an `ILogger` or `ILoggerFactory` (from the `.Abstractions` package), or a tiny interface the host adapts;
  the default does nothing.
- Structured logs carrying a migration id and a correlation id. No secrets and no personal data.
- A support bundle the customer can export.
- Practise: 06-02.

**A7. SDK vs host responsibilities.** *Probing:* boundaries.
- SDK: the workflow, transport, retries, validation, state persistence (with a pluggable store), and the progress
  and result types.
- Host: all UI, dialogs and prompts. Credential storage behind an interface the SDK defines (with a sensible
  Windows default).
- The SDK never shows a window and never references a UI framework.
- Practise: 09-02, 09-03.

**A8. Testing against cloud services.** *Probing:* test design.
- Inject the HTTP layer (`HttpMessageHandler`), and use fakes or a local mock server (like `shared/MockServer`) with fault injection.
- Control time (`TimeProvider`) so retry tests take no real time; test the cancellation paths.
- A separate, smaller set of contract tests against the real API; a sample host app for integration testing.
- Practise: 13-02, 05-01.

## B. The migration workflow

**B1. Design the migration.** *Probing:* workflow and state design; asking good questions.
- Clarify: company size, multi-user or not, what moves (company data, forms, attachments), who starts it, what
  happens to the desktop data afterwards.
- Stages: sign in → pre-migration checks → provision or find the environment → take a consistent snapshot
  (backup) → chunked, resumable upload → server-side restore and validation → reconciliation → switch over.
- Explicit states, including Failed, Cancelled and NeedsAttention. Every step idempotent. A local checkpoint,
  with the server as the source of truth.
- Practise: 10-01, 10-02, 16-01.

**B2. Closed at 60 %.** *Probing:* recovery.
- A durable checkpoint written atomically (temp file, then rename) holding the migration id, stage, upload
  session and file hash.
- On restart, reconcile with the server: which parts arrived, and whether the restore already started. Resume
  rather than restart.
- If the source changed since the snapshot, detect it (hash, timestamp) and decide deliberately.
- Practise: 10-03, 07-02.

**B3. Other users keep posting.** *Probing:* consistency, and talking to users.
- Ask: can we require exclusive access? Is there a cut-over window?
- Migrate a consistent snapshot (a backup), not live files that are still changing.
- Stop new changes to the desktop data during or after the migration (all users logged out, or read-only after
  cut-over), so the two copies can't drift apart.
- Detect changes made after the snapshot, and warn or re-run. Explain it to the accountant in plain language.
- *Senior:* catching up on the changes made during the migration (a delta migration) instead of blocking everyone.
- Practise: 04-02 (thinking in interleavings), 16-04.

**B4. Pre-migration checks.** *Probing:* validation that fits the domain.
- Examples: run the product's own data-integrity check (Sage 50 Accounts has *Check Data*); a supported product
  version; enough disk space for the backup; settings the cloud edition doesn't support (like SmartPosting in
  the US edition); other users logged out; network and proxy reachable; credentials valid; the company not
  already migrated.
- Structure: independent rules producing a report of errors and warnings, run before anything is uploaded.
- Practise: 10-02, 07-04.

**B5. Proving it was correct.** *Probing:* reconciliation, which an accounting company cares about deeply.
- Record counts per entity (customers, suppliers, invoices, nominal accounts).
- Financial totals that must match: the trial balance, aged debtors and creditors.
- The uploaded file's hash matches; the server produces a restore report; the comparison is automatic, with a
  summary the customer can see.
- The source stays untouched until the customer confirms.
- Practise: 10-01, 01-01.

**B6. Verification fails.** *Probing:* failure the user can act on.
- A clear message: what failed, whether the data is safe (the source is untouched), and what to do next.
- Re-run verification, or re-send only what failed. A support reference (correlation id) and a support bundle.
- State: NeedsAttention, not "Failed" and not "Unknown error".
- Practise: 01-03, 10-01, 06-02.

**B7. Adding a stage.** *Probing:* extensibility.
- A stage abstraction (name, `ExecuteAsync(context, token)`, idempotent, can be conditional), composed as a pipeline.
- The existing tests are the contract. The new stage keeps its own state in the checkpoint; version the
  checkpoint format.
- Practise: 10-02.

**B8. 30 GB and twelve years.** *Probing:* scale arithmetic.
- 30 GB at a 10 Mbit/s upload is about 6.7 hours before any retries. Say the arithmetic out loud.
- Streaming, never buffering; resumable chunks; honest time estimates; overnight runs; stopping the PC from
  sleeping; server-side restore time.
- Compression only if the data is not already compressed; measure first.
- Practise: 16-02, 07-01.

## C. Moving the files

**C1. 20 GB over broadband.** Chunked and resumable (an upload session, or blocks straight to Blob Storage);
streamed from disk, hashing while reading; bounded memory; a few chunks in parallel; retries per chunk with
backoff; throttled progress; cancellation. Practise: 07-01, 07-02.

**C2. Dies at 80 %.** Ask the *server* what it has, and resume from there; don't trust a local counter. If the
session expired, start a new one. Verify the hash at the end. Keep a retry budget. Practise: 07-02, 10-04.

**C3. Status codes.**
- 400: our bug or bad data. Don't retry; report the details.
- 401: refresh the token once and retry; if it happens again, it is an authentication failure.
- 403: no permission or subscription. Don't refresh; tell the user.
- 409: often "already exists". Check whether it was our own earlier attempt, and if so treat it as success.
- 429: wait for `Retry-After`, then lower concurrency.
- 503: transient. Back off with jitter, up to a cap.

Practise: 08-01, 06-01.

**C4. Timed out, may have worked.** List the possible states (never arrived; arrived but not processed;
processed and the response lost). Resolve it by *asking the server*, or by retrying with the same idempotency key
so the server removes the duplicate. The key comes from migration id + file identity + content hash, never a new
GUID per attempt. Practise: 10-04, 07-03.

**C5. Same file?** SHA-256 computed while reading, sent with the upload and checked by the server (a mismatch is
rejected); per-chunk checksums (Blob Storage supports MD5 and CRC64); checked again after the restore. TLS
protects the transfer, not a bad local read or a truncated file. Practise: 02-06, 07-01.

**C6. Monday 9:00.** A retry storm and a provisioning backlog. Client: jittered backoff, `Retry-After`, a retry
budget, a circuit breaker, a server-controlled start time or concurrency. Server: queued provisioning,
autoscaling, admission control, rolling the campaign out gradually. Practise: 08-03, 16-04.

**C7. Laptop sleeps.** Connections fail. On wake, resume from the server's state. Optionally ask Windows not to
sleep during a migration (`SetThreadExecutionState`), telling the user. Checkpoint often. Practise: 07-02, 16-03.

**C8. TLS-inspecting proxy.**
- **Never** turn off certificate validation (`ServerCertificateCustomValidationCallback => true`). That removes
  the protection for every customer, and it is a classic interview red flag.
- Instead: use the system proxy settings, and pass default credentials for proxies that require sign-in. The
  customer's IT installs their inspection certificate in the Windows certificate store, where .NET trusts it.
- Give IT a clear diagnostic listing which hosts must be allowed.
- If the SDK pins certificates, that conflicts with inspection; discuss the trade-off.

Practise: 16-03 (discussion).

## D. Sign-in and provisioning

**D1. Desktop sign-in.** A desktop app is a public client: authorization code with PKCE in the system browser, so
the SDK never sees the password. Tokens scoped to the migration APIs; refresh tokens. *Strong:* the desktop
product may already be signed in to the customer's Sage account, so the SDK should accept a token from the host
through an interface rather than run its own sign-in. Practise: 06-01, 06-03, quick-fire F5.

**D2. Token expires with eight uploads in progress.** One shared refresh for all of them (single-flight). Each
rejected request retried once with the new token, never in a loop. Refresh ahead of expiry, allowing for clock
differences. A 403 never triggers a refresh. Practise: 06-01.

**D3. Storing tokens.** Store as little as possible: the refresh token only. Use DPAPI (`ProtectedData`,
CurrentUser scope), the Windows Credential Manager, or MSAL's cache. Never plain files, the registry or logs.
Clear them on sign-out. Practise: 06-02, 06-03.

**D4. Provisioning takes minutes.** A long-running operation: 202 plus an operation id, then poll with backoff and
`Retry-After`. Save the operation id so a restart keeps waiting instead of creating another environment. A time
budget. Decide what cancelling means: does it stop the provisioning? The UI shows the stage and an honest estimate.
Practise: 08-02.

**D5. 409 on provisioning.** It may be our own earlier attempt whose response was lost: fetch it and reuse it. Or
someone else created it: check it belongs to this customer and company before reusing. Never create a duplicate.
Practise: 08-02, 10-04.

**D6. Personal and financial data.** UK GDPR: collect and keep only what is needed; encryption in transit and at
rest; data residency (a UK region); no personal data in logs or telemetry (redact it); delete local temporary
copies such as backups; access control and an audit trail. Practise: 06-02.

## E. Inside the desktop app

**E1. Freeze on Migrate.** The classic cause is blocking on async code on the UI thread (`.Result`, `.Wait()`),
which deadlocks. Otherwise, long synchronous work on the UI thread. Diagnose with the debugger (Break All,
Parallel Stacks). Fix: async all the way up; `ConfigureAwait(false)` inside the SDK; `Task.Run` in the host for
CPU-bound work. Practise: 02-04.

**E2. Smooth progress.** An `IProgress<T>` created on the UI thread, or the dispatcher. Throttle updates (every
~250 ms or every percent). Immutable snapshots bound to a view model. Never marshal every chunk. Practise: 11-02.

**E3. Background exceptions.** An unhandled exception on a `Thread` or thread-pool thread ends the process, and
so does one escaping `async void`. So: no `async void`, no fire-and-forget; every piece of background work is a
`Task` returned to the caller, and background loops catch errors and report them through the result. Practise:
02-01, 03-03.

**E4. Cancel mid-chunk.** Pass the token down to stream reads and to `HttpClient`, which aborts the request in
progress; there is no need to wait for the chunk to finish. The state becomes Cancelled, and the migration can
resume later. The UI shows "Cancelling…" until the task completes. Practise: 03-02.

**E5. Memory grows.** Compare memory snapshots taken before and after, and follow the paths to root. Suspects:
event handlers on long-lived SDK objects, static caches, timers, undisposed streams and responses, a new large
buffer per chunk (Large Object Heap). Practise: 11-03, 02-03.

**E6. MAUI instead of WPF.** Threading through `MainThread` or the dispatcher. On mobile the OS can suspend or
kill the app, which the resumable design already allows for. File access through platform pickers and
permissions, and background-execution limits on mobile. MAUI on Windows is closer to WPF. The SDK is modern .NET
there, hence multi-targeting. Practise: 12-01, 12-03, 12-04.

## F. Accounting data

**F1. Money.** Use `decimal`, never `double`. Keep rounding rules consistent between the systems: .NET's
`Math.Round` rounds to even by default (banker's rounding), which may not match. Watch currency precision, VAT
rounded per line vs per invoice, and values converted through `double` or JSON floats on the way. Reconcile the
totals. Practise: quick-fire E6.

**F2. Dates.** `DateTime` values with `Kind` Unspecified from old data; time-zone conversions that move a date
across midnight. An invoice dated 1 April that becomes 31 March lands in the wrong VAT quarter. Financial years
that don't start in January, closed periods, GMT and BST.

**F3. £, é, ß and code pages.** Legacy text saved in Windows-1252 but read as UTF-8, or the reverse, gives
garbage such as `Â£`. File names with non-ASCII characters in HTTP headers must be percent-encoded. Unicode
normalisation (NFC vs NFD) matters when comparing names. Test with real customer data. Practise: 07-04, 02-06.

**F4. Graphical assets.** Validate files by their content (magic bytes), not the extension. Limit size and image
dimensions (decompression bombs), and handle corrupt files. De-duplicate by content hash. Keep the links to their
records. Never decode full-size images on the UI thread. `System.Drawing` is Windows-only on modern .NET, which
matters for a cross-platform SDK. Practise: 14-02, 09-03, 07-04.

## G. Azure

**G1. Direct to Blob Storage, or through the API?** Direct, with a short-lived SAS from the API, scales better and
keeps the API's bandwidth and cost down; Blob Storage's blocks make resuming natural. Going through the API gives
simpler control and validation during the upload, but makes the API a bottleneck. The common answer: the API
controls the process, and Blob Storage carries the data. Practise: 16-06.

**G2. SAS tokens.** Scoped to one blob or container, write-only, HTTPS only. They expire within minutes to
hours, and the SDK asks for a fresh one when one runs out mid-upload. Prefer a user delegation SAS. Practise: 16-06.

**G3. Where state lives.** Both. The server is authoritative for what was received and processed; the client
records what it intended and how far it got. When they disagree, the server wins; reconcile on start-up.
Practise: 10-03, 10-04.

**G4. Server-side restore.** The API receives "upload complete" and puts a message on a queue (Service Bus). A
worker restores the company into the hosted environment and updates the status, which the client polls. Messages
can arrive more than once, so processing must be idempotent; add dead-lettering and timeouts. Durable Functions is
an option. Practise: 08-02, 16-06.

**G5. Knowing migrations fail in the field.** Telemetry from the SDK (Application Insights, for example), with the
customer's consent: migrations started, stage durations, failures by error code, SDK version. Correlation ids
shared with the server logs, dashboards and alerts on success rate. No personal data. Practise: 06-02.

## H. About you

No model answers; they must be the candidate's own. Use `prep/story-bank.md`: specific, "I" not "we", a number
where possible, and a lesson. For **H9**, know the products (Sage 50 Accounts, Sage 200, Sage Intacct, Sage
Accounting) and the hosted Sage 50 on Azure, and say why a migration SDK in particular is interesting.
