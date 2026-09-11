# Job Coverage

How this repository maps onto the requirements of the target role: building a **migration agent SDK** in .NET,
consumed by a WPF desktop application and a .NET MAUI application, moving files and metadata into a cloud
platform over unreliable networks.

Coverage is judged by how much of the requirement a candidate would have to demonstrate to finish the listed
exercises — not by how many exercises mention it.

| Job requirement | Exercises | Coverage |
|---|---|---|
| .NET ecosystem / modern C# | 01-01, 01-02, 01-03, 02-01…02-06, 03-01…03-03 | High |
| WPF | 11-01, 11-02, 11-03, 11-04, 14-02, 15-02, 09-03 | High |
| .NET MAUI | 12-01, 12-02, 12-03, 12-04, 16-01 | High |
| .NET Standard 2.0 / multi-targeting | 09-03, 09-04, 09-05, 16-05 | High |
| SDK / library development | 09-01…09-05, 16-01, 16-05, 15-01, 05-01 | Very High |
| API integrations | 05-01, 05-02, 07-03, 08-01, 08-02, 02-06 | High |
| Authentication | 06-01, 06-02, 06-03, 02-05, 16-06 | High |
| File transfer | 07-01, 07-02, 07-03, 07-04, 10-03, 16-02, 16-03 | Very High |
| Retry / recovery | 08-01, 08-02, 08-03, 10-03, 10-04, 07-02, 16-03 | Very High |
| Migration workflows | 10-01, 10-02, 10-03, 10-04, 16-01, 16-04 | Very High |
| Error handling | 01-03, 02-02, 03-03, 08-01, 10-01, 15-01, 15-02 | Very High |
| Performance | 14-01, 14-02, 03-01, 07-01, 16-02 | High |
| Azure / cloud | 16-06, 08-02, 16-03 | Medium |
| Desktop integration | 11-01…11-04, 12-04, 09-03, 15-02, 02-03 | High |
| Testing | 13-01, 13-02, 09-03, 04-01, plus the 41 exercises with test suites | High |
| Concurrency | 04-01, 04-02, 04-03, 03-01, 06-01, 16-04, 08-03 | High |

## Why each exercise earns its place

### .NET ecosystem and modern C#

| Exercise | Why it is relevant |
|---|---|
| 01-01 Project totals are wrong | Reading unfamiliar LINQ over real result data, where equality and keys decide whether a customer's report is right |
| 01-02 Where do files come from? | The SDK must read from a folder today and from an archive or a network share tomorrow. This is the abstraction that decision hangs on |
| 01-03 "Provisioning failed" | Modelling failure as data rather than as an exception, which is the difference between a usable SDK result and a stack trace shown to a customer |
| 02-01…02-06 | Six support tickets covering the six mistakes that actually reach production in this kind of product |

### WPF and desktop integration

| Exercise | Why it is relevant |
|---|---|
| 11-01 Status panel never updates | The binding and `INotifyPropertyChanged` failures every WPF codebase has |
| 11-02 Progress from a background migration | The core desktop problem for this job: work on a worker thread, UI on the dispatcher, and not flooding it |
| 11-03 The app that grows | Event and timer leaks — why the consultant's app is at 2 GB after a day of migrations |
| 11-04 A control only one app can use | Reusability in the UI layer, mirroring the reusability the SDK itself needs |
| 14-02 The gallery freezes | Virtualization and image decoding: the two things that make WPF lists unusable at customer scale |
| 15-02 Review the migration window | Reviewing desktop code for threading, lifetime and testability |
| 09-03 The core that needs a window | Why the SDK must not reference a UI framework, enforced by a test |

### MAUI

| Exercise | Why it is relevant |
|---|---|
| 12-01 The detail flow a WPF developer wrote | Exactly the transition this role involves: WPF habits applied to Shell navigation, and what breaks |
| 12-02 The board that doesn't update | What "the CollectionView is broken" can actually mean, including the main-thread rule that differs from Windows |
| 12-03 Killed in the background | Mobile lifecycle: the OS can kill the app with no callback, so a long migration must be built to survive it |
| 12-04 A view model you cannot test | Platform statics vs injected abstractions — the difference between a view model that can be tested on a build server and one that needs a device |

### SDK and library design

| Exercise | Why it is relevant |
|---|---|
| 09-01 Design the public API | The central skill of the job, done as a design conversation |
| 09-02 Review: MigrationManager | Recognising bad SDK design in existing code, and saying which of its problems matter most |
| 09-04 The customer's .NET Framework 4.8 application | .NET Standard 2.0 in practice: polyfills, package graph, and a host that proves it works |
| 09-05 / 16-05 Shipping version 2 | Binary, source and behavioural breaking changes for software that ships to machines you cannot update |
| 16-01 Design the migration SDK | The whole architecture, including the constraint that one SDK serves WPF and MAUI |

### API integration, authentication and file transfer

| Exercise | Why it is relevant |
|---|---|
| 05-01 The migration API client | Building the HTTP layer the SDK stands on, against the mock platform |
| 05-02 The server upgrade | Tolerant readers and api-version pinning: a desktop fleet cannot be upgraded in lockstep with the server |
| 06-01 Token expires mid-migration | A migration outlives its token. Single-flight refresh, without a stampede |
| 06-02 Secrets in the support logs | Redaction, and what a support bundle may contain |
| 06-03 The token cache | Caching with a clock you can control in tests |
| 07-01 The 100 GB file | Streaming, hashing and progress without loading the file into memory |
| 07-02 Large files on a flaky connection | Chunked, resumable upload with the server as the source of truth |
| 07-03 Duplicate documents | Idempotency keys derived from identity and content |
| 07-04 Real customer data | Zero-byte files, Unicode names, a corrupted file, a missing file — the data customers actually have |

### Reliability, recovery and workflow

| Exercise | Why it is relevant |
|---|---|
| 08-01 The platform has bad minutes | Retry classification, jitter, `Retry-After`, and a budget |
| 08-02 Provisioning takes minutes | Long-running operations: 202, polling, and not hanging the UI |
| 08-03 Retry storm | What well-meant retries do to a recovering service, with a simulator that produces real numbers |
| 10-01 A migration that says "Completed" | The state machine, and the states people forget |
| 10-02 Two more stages | Changing a workflow safely, with the existing tests as the contract |
| 10-03 The app died at 63 % | Checkpointing, atomic writes and reconciliation after a crash |
| 10-04 The upload that might have worked | The ambiguous result: the request left, the response did not |

### Testing, performance and judgement

| Exercise | Why it is relevant |
|---|---|
| 13-01 The test suite nobody trusts | Making an unreliable suite deterministic — shared state, ordering, sleeps |
| 13-02 Tests for retry and cancellation | Testing time-dependent and cancellation behaviour without waiting for real time, and what that uncovers |
| 14-01 Planning takes eleven minutes | N+1 calls and quadratic work, measured rather than guessed |
| 15-01 Review this PR | Prioritising and communicating findings — the day-to-day form of all of the above |
| 16-02 / 16-03 / 16-04 | Scale, unreliable networks and concurrent migrations as design conversations |
| 16-06 Moving the platform to Azure | Blob Storage, SAS lifetimes, queues and managed identities as architecture, not trivia |

## Where coverage is deliberately thin

- **Azure (Medium).** Discussed, never provisioned: no subscription is required anywhere. 16-06 covers the
  design decisions that affect the SDK; anything requiring a real Azure account is out of scope.
- **Databases.** The platform is behind an HTTP API, so there is no ORM or schema work. 14-01 covers the
  N+1 pattern in its API form.
- **CI/CD, packaging and installers.** Mentioned in 16-05 (shipping to laptops), not exercised.
- **Front-end styling.** The WPF and MAUI exercises are about behaviour, threading and structure, not design.
