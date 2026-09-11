# Quick-fire Answers

Model answers for `prep/quick-fire-questions.md`. Read them **after** answering out loud. Each one is the core of
a solid answer, and a real example from your own work makes it strong. Where an answer ties to an exercise, try
the exercise too.

## A. C# and .NET fundamentals

**A1. Value vs reference types.** Value types (`struct`, `int`, enums) hold their data directly. A local lives
on the stack; a field lives inside its containing object. Reference types (`class`, `string`, arrays) live on the
heap, and variables hold a reference to them. When you pass a value type to a method it is copied, so the caller
is unaffected unless you use `ref`, `in` or `out`. When you pass a reference type, the reference is copied: the
method can change the object, but reassigning the parameter does not affect the caller. *Strong:* "stack vs heap"
is an implementation detail; what matters is copy vs share. Copying large structs costs time.

**A2. `record`.** It gives you value-based equality, a useful `ToString`, `with` expressions (a changed copy)
and positional syntax with deconstruction. Good for DTOs, results and messages. `record struct` is the value-type
version, for small data where you want to avoid allocations; `readonly record struct` also makes it immutable.
*Watch out:* equality of a record containing a `List<T>` compares the list by reference.

**A3. Interface vs abstract class.** An interface is a contract: a class can implement many, and it holds no
state. An abstract class shares implementation and state, a class can inherit only one, and you can add
non-abstract members later without breaking subclasses. *SDK angle:* adding a member to a public interface breaks
everyone who implements it (default interface members are not available in `netstandard2.0`). For extension points
you expect to grow, use an abstract base class or add a new interface.

**A4. Overloading vs overriding.** Overloading means the same name with different parameters, resolved at
compile time. Overriding means a `virtual` or `abstract` member in the base class is replaced with `override` in
a derived class, resolved at runtime. `new` *hides* the base member, so which one is called depends on the
variable's declared type. `sealed` stops further overriding, or stops a class being inherited.

**A5. Boxing.** Converting a value type to `object` or to an interface: a heap allocation plus a copy. It sneaks
in through non-generic collections, casting a struct to an interface, some older formatting and `Enum` APIs, and
LINQ over structs through interfaces. It only matters on hot paths.

**A6. `IEnumerable` vs `IQueryable`.** `IEnumerable<T>` runs LINQ in memory, with delegates. `IQueryable<T>`
builds expression trees that a provider translates (EF → SQL). Deferred execution means the query runs when it is
enumerated, not when it is written. It bites when you enumerate twice (two database queries, a file read twice,
side effects repeated), when the source changes in between, or when the query runs after its context is disposed.
Call `ToList()` when you mean "now, once".

**A7. `IDisposable`.** Implement it when you own unmanaged resources or other disposables: streams,
`HttpResponseMessage`, timers, subscriptions to a longer-lived publisher. `using` compiles to `try/finally` with
`Dispose()` in the `finally`. Use `IAsyncDisposable` and `await using` when cleanup is itself asynchronous, e.g.
flushing a stream to the network. A finalizer is only for directly owned unmanaged handles; prefer `SafeHandle`.

**A8. GC and the LOH.** The GC is generational: gen 0 holds short-lived objects, gen 2 long-lived ones. Most
objects die young, and collections compact memory. Objects of 85,000 bytes or more go straight onto the Large
Object Heap, which is collected with gen 2 and not compacted by default, so memory fragments and grows. *File
transfer:* `File.ReadAllBytes` on a large file, or a new 1 MB buffer for every chunk, causes LOH churn or
`OutOfMemoryException`. Stream the data and reuse buffers, e.g. with `ArrayPool<byte>.Shared`. See 07-01.

**A9. Rethrowing.** `throw;` rethrows and keeps the original stack trace. `throw ex;` resets the stack trace, so
you lose where the exception started. Wrapping, `throw new MigrationException("while uploading X", ex)`, adds
context and keeps the original as `InnerException`; that is right at layer boundaries.
`ExceptionDispatchInfo.Capture(ex).Throw()` rethrows later without losing the trace.

**A10. Delegates and events.** A delegate is a type-safe reference to a method; `Func` and `Action` are the
built-in generic ones. An event is a delegate that only its owner can raise; others can only `+=` and `-=`. *The
leak:* a long-lived publisher keeps every subscriber alive through its delegate list. For example, a view model
subscribed to a singleton service's `ProgressChanged` event is never collected. Unsubscribe on dispose or unload,
use weak events (`WeakEventManager` in WPF), or shorten the publisher's lifetime. See 11-03.

**A11. Nullable reference types.** Compile-time annotations and warnings only; there are no runtime checks.
Deserialized data, reflection and unannotated libraries can still give you `null`, and `!` silences the warning.
At a public SDK boundary, still check arguments (`ArgumentNullException`), because callers may not use nullable
annotations at all, for example .NET Framework host code.

**A12. Generics.** Constraints (`where T : class`, `struct`, `new()`, `notnull`, a base class, an interface)
let you call members on `T` and document what `T` must be. Covariance (`out T`): an `IEnumerable<string>` can be
used as an `IEnumerable<object>`. Contravariance (`in T`): an `Action<object>` can be used as an
`Action<string>`. Variance applies only to interfaces and delegates, and only with reference types.

**A13. Strings.** Strings are immutable: every change creates a new string. Concatenating in a loop is
quadratic, so use `StringBuilder`. For a handful of pieces, `+` or interpolation is fine. Immutability makes
strings safe to share between threads and to use as dictionary keys.

**A14. `const` vs `readonly`.** A `const` is a compile-time value that gets *copied into the calling
assemblies*. `readonly` is set once per instance in the constructor; `static readonly` is set once per type at
runtime. *SDK trap:* change a public `const` in v2 and apps compiled against v1 keep the old value until they
recompile. Use `static readonly` or a property for anything that might change.

## B. Object-oriented design

**B1. The four pillars.** Encapsulation: hide state behind behaviour (a migration session that only exposes
`StartAsync` and `CancelAsync`). Abstraction: callers depend on "a source file", not on `FileInfo`. Inheritance:
share behaviour, used sparingly. Polymorphism: different upload strategies behind one interface. The interviewer
wants *your* examples.

**B2. SOLID.** Single responsibility: one reason to change. Open/closed: add a workflow stage without editing a
`switch` (10-02). Liskov: a subtype keeps the base type's promises; a read-only store that throws from `Save`
breaks that. Interface segregation: small, focused interfaces. Dependency inversion: business logic depends on
abstractions, not on HTTP or the file system. *Strong:* name a place where SOLID was over-applied, such as an
interface for every class.

**B3. Composition vs inheritance.** Prefer composition: build behaviour from collaborators (an uploader plus a
retry policy plus a checksum calculator). It is flexible and testable, and avoids fragile base classes. Use
inheritance for a true "is-a" relationship with shared rules, or when the framework requires it
(`DelegatingHandler`, WPF controls).

**B4. Patterns.** MVVM separates the view from state and logic, which makes view models testable. Repository
hides data access, but can become a leaky pass-through over EF. Factory centralises creation. Strategy makes
algorithms swappable (retry policies). Observer is events, `IObservable` and `INotifyPropertyChanged`. Decorator
wraps behaviour; the `DelegatingHandler` pipeline is a chain of decorators. Patterns make code worse when there
are layers with only one implementation, or singletons acting as hidden global state.

**B5. Hard to test.** Hidden dependencies: creating `HttpClient` or services with `new` inside the class, static
calls (`DateTime.Now`, `File.*`, the dispatcher), singletons and global state, long methods that mix I/O with logic,
and time or randomness you cannot control. Inject abstractions instead: `TimeProvider`, a file-access interface,
an `HttpMessageHandler`. See 13-02 and 12-04.

## C. Async, threading and concurrency

**C1. `await`.** If the task has not finished, the method returns to its caller and registers the rest of itself
as a continuation (the compiler builds a state machine). When the task finishes, the continuation runs on the
captured `SynchronizationContext` if there is one (the UI thread in WPF and MAUI), otherwise on the thread pool.
A `SynchronizationContext` is an abstraction for "where to run code"; WPF's posts work to the dispatcher.

**C2. The deadlock.** On the UI thread, `.Result` blocks the thread. The awaited operation's continuation wants
to resume on the UI thread, which is blocked, so both wait forever. Library code uses `ConfigureAwait(false)` so
its continuations do not need the caller's context. That avoids this deadlock for callers who block, and saves
needless trips to the UI thread. The real fix in app code is async all the way up. An SDK cannot control whether
its host blocks, so it must not rely on the host behaving. See 02-04.

**C3. `async void`.** Only for event handlers, and similar top-level entry points that must match a `void`
signature. Its exceptions go straight to the `SynchronizationContext`, which usually crashes the app, and nobody
can await its completion. In commands, catch and surface errors, or use an async command (G4).

**C4. `Task.Run`.** Right in an app, for moving CPU-bound work (hashing, image processing) off the UI thread. A
warning sign when it wraps I/O that is already async, or when used inside library code: an SDK method that calls
`Task.Run` hides the cost and takes thread-pool threads from the host. Let the caller decide. Using `Task.Run`
around sync-over-async to dodge a deadlock is a band-aid, not a fix.

**C5. `Task.WhenAll` with a failure.** `await` throws only the *first* exception. The combined task's
`.Exception` is an `AggregateException` with all of them, and each individual task has its own status. To report
per-item results, inspect each task, or have each operation return a result instead of throwing. `WhenAll` waits
for everything: one failure does not cancel the rest unless you share a `CancellationTokenSource`. See 03-03.

**C6. Cancellation.** It is cooperative: the caller signals through a `CancellationTokenSource`; the code checks
the token, or passes it to APIs that observe it. A method that accepts a token owes its caller this: pass it to
every async call below it (HTTP, stream reads, `Task.Delay`), stop promptly, throw `OperationCanceledException`
rather than report partial success, leave state consistent, and not count cancellation as a failure in logs or
retries. Combine a user cancel with a timeout using `CreateLinkedTokenSource` and `CancelAfter`. See 03-02.

**C7. Synchronisation tools.** `lock` gives mutual exclusion for short synchronous sections, and you cannot
`await` inside it. `SemaphoreSlim` works with async (`WaitAsync`) and can allow N at a time, so it also
throttles. `Interlocked` does atomic operations on a single variable (counters, `CompareExchange`) without a lock.
Concurrent collections (`ConcurrentDictionary`, `ConcurrentQueue`, `Channel<T>`) are thread-safe containers, but
compound check-then-act operations still need care: `GetOrAdd`'s factory can run more than once.

**C8. Race and deadlock.** A race: the result depends on the timing of interleaved operations, e.g. two uploads
read-modify-write the same progress state and one update is lost (04-01). A deadlock: two parties each wait for
the other, e.g. the UI thread blocked on `.Result` while the continuation needs the UI thread (02-04), or two
locks taken in opposite orders. Give one of each from your own experience.

**C9. `Task` vs `ValueTask`.** `ValueTask` avoids an allocation when the result is often already available
(cached values, buffered reads). Rules: await it only once, never concurrently, and never read `.Result` before
it completes. Default to `Task` and use `ValueTask` on hot paths. In `netstandard2.0` it comes from the
`System.Threading.Tasks.Extensions` package.

**C10. 500 uploads, 8 at a time.** `SemaphoreSlim(8)` with `WaitAsync`/`Release` around each upload;
`Parallel.ForEachAsync` with `MaxDegreeOfParallelism` (.NET 6+, not in `netstandard2.0`); a `Channel<T>` read
by 8 worker tasks; or TPL Dataflow's `ActionBlock`. Also mention cancellation and per-item errors. *Strong:* on
.NET Framework, the HTTP connection limit (`ServicePointManager.DefaultConnectionLimit`, 2 by default in desktop
apps) can silently cap you at 2 anyway. See 03-01.

## D. Dependency injection and app structure

**D1. Lifetimes.** Singleton: one instance for the container's lifetime. Scoped: one per scope (per request in
ASP.NET Core; in a desktop app you create scopes yourself, e.g. per migration or per window). Transient: a new
one every time. A captive dependency is a singleton holding something shorter-lived, which then lives forever;
e.g. a singleton capturing a per-customer context uploads files to the wrong customer (02-05).
`ValidateScopes` and `ValidateOnBuild` catch some of these.

**D2. DI in WPF and MAUI.** WPF has no built-in container: use the Generic Host
(`Microsoft.Extensions.Hosting`) in `App.OnStartup`, and resolve the main window and view models from it rather
than through a static service locator. MAUI has DI built in: register services, pages and view models in
`MauiProgram.CreateMauiApp`, and Shell constructs registered pages with constructor injection. See 12-01.

**D3. DI in an SDK.** Do not force it: the host may use another container or none at all. Offer a plain
constructor or builder that works without DI (`new MigrationClient(options)`), plus an optional
`IServiceCollection` extension (`AddMigrationSdk(...)`) for hosts that use Microsoft's DI. Depend only on the
`.Abstractions` packages.

**D4. What an SDK takes from its host.** An `ILoggerFactory` or `ILogger` from
`Microsoft.Extensions.Logging.Abstractions`, defaulting to `NullLogger`, so the host decides where logs go.
Never write to the console or a file unasked. Configuration arrives as an options object, validated at
construction, rather than read from `app.config` or environment variables. Never log secrets (06-02).

## E. Data and SQL

**E1. Joins and grouping.** `INNER JOIN` keeps only rows that match on both sides. `LEFT JOIN` keeps every row
from the left side, with NULLs where nothing matches; customers with no invoices are
`LEFT JOIN ... WHERE i.Id IS NULL`. `WHERE` filters rows before grouping; `HAVING` filters groups after
`GROUP BY`, e.g. `GROUP BY CustomerId HAVING COUNT(*) > 10`.

**E2. Second-highest invoice total per customer.**

```sql
SELECT CustomerId, Total
FROM (
    SELECT CustomerId, Total,
           DENSE_RANK() OVER (PARTITION BY CustomerId ORDER BY Total DESC) AS Rnk
    FROM Invoices
) ranked
WHERE Rnk = 2;
```

Talk about ties (`DENSE_RANK` vs `ROW_NUMBER`) and customers with only one invoice (they get no row). The older
approach uses a correlated subquery with `MAX`.

**E3. Indexes.** A clustered index is the table's physical order; there is one per table, usually the primary
key. Non-clustered indexes are separate structures pointing at rows; a table can have many, and `INCLUDE` makes
one covering. Indexes hurt by slowing inserts, updates and deletes, and by using storage. Too many overlapping
indexes, low-selectivity columns, and functions wrapped around a column in `WHERE` all waste them.

**E4. Transactions.** A transaction is an all-or-nothing unit (ACID). Isolation levels: Read Uncommitted allows
dirty reads, i.e. reading another transaction's uncommitted changes. Read Committed is SQL Server's default.
Then Repeatable Read, Serializable, and Snapshot (row versioning). The trade-off is consistency vs concurrency
and blocking.

**E5. Entity Framework.** With tracking, EF snapshots entities to detect changes, which updates need; use
`AsNoTracking()` for read-only queries (faster, less memory). N+1: load a list, then lazy-load a navigation
property per item, so one query per row. Fix it with `Include`, a projection (`Select`), or batching (14-01 shows
the same pattern over an API). Migrations version the schema from code: review the generated SQL, and watch for
operations that lose data.

**E6. Money.** `double` is binary floating point: `0.1 + 0.2 != 0.3`, and rounding errors accumulate in sums, so
an accounting total can be a cent out and fail to reconcile. `decimal` is base-10 with 28–29 significant digits,
exact for currency. Also make rounding rules explicit (`MidpointRounding`), store money as a decimal type in the
database, and watch for JSON consumers in other languages turning decimals into floats. *This is exactly the kind
of thing an accounting company will ask.*

## F. Web APIs and HTTP

**F1. Status codes.** 200 OK. 201 Created (with `Location`). 202 Accepted (long-running; poll for the result).
204 No Content. 400: the request is wrong, so do not retry. 401: not authenticated (missing or expired token), so
refresh once and retry. 403: authenticated but not allowed, so do not refresh, report it. 404 Not Found. 409
Conflict: a state problem or duplicate, and it may mean the work is already done. 422: semantically invalid (e.g. a
checksum mismatch). 429: rate limited, so honour `Retry-After`. 500: a server error, maybe transient. 502 and 503:
transient, retry with backoff. 504: gateway timeout, which is *ambiguous*; the server may have done the work
(10-04). See 08-01 and 06-01.

**F2. Idempotency.** GET, HEAD, PUT, DELETE and OPTIONS are idempotent: repeating them has the same effect as
doing them once. POST and PATCH are not, by default. Retrying a POST after a timeout can create duplicates (two
migrations, a file stored twice). Use idempotency keys, where the server remembers the result for each key, or
check the server's state before retrying. See 07-03.

**F3. `HttpClient`.** Reuse a long-lived instance, or use `IHttpClientFactory` in hosts that have DI. A new
`HttpClient` per request exhausts sockets (TIME_WAIT). One kept forever misses DNS changes; on modern .NET set
`SocketsHttpHandler.PooledConnectionLifetime`. In an SDK, either accept an `HttpClient` or handler from the host,
or own one for the SDK's lifetime, and never dispose one you did not create. Set timeouts deliberately: the
default is 100 seconds, which is wrong for large uploads, so use per-operation cancellation instead. See 05-01.

**F4. REST and versioning.** REST means resources, standard methods and status codes; RPC-style means operations
as endpoints (`POST /startMigration`). Versioning can go in the URL (`/v2/`), in a header or query
(`api-version=2024-05-01`, as Azure does), or in the media type. With desktop clients the server must support old
versions for a long time, and clients should be tolerant readers that ignore unknown fields and enum values. See 05-02.

**F5. Desktop sign-in.** The client-credentials flow is machine-to-machine, and the client holds a secret; it
suits confidential clients such as servers. A desktop app is a *public* client: any secret shipped in it can be
extracted. Use the authorization code flow with PKCE through the system browser (MSAL, for Entra ID), and store
refresh tokens securely per user (DPAPI, the OS credential store, or MSAL's token cache). The mock server in this
repository uses client credentials only to keep the exercises simple.

**F6. A resilient client.** A timeout on every call, and an overall time budget. Retry only transient failures
(network errors, 408, 429, most 5xx), and only for idempotent or idempotency-keyed operations. Exponential
backoff with jitter, so clients do not retry in lockstep (08-03). Cap the attempts and the total time. Honour
`Retry-After`. A circuit breaker to fail fast while a dependency is down. Honour cancellation during the delays.
Know why you would or would not use a library (Polly, `Microsoft.Extensions.Http.Resilience`).

## G. Desktop: WPF and MAUI

**G1. Binding.** A binding connects a target (a dependency property on a control) to a source property, usually
on the view model in `DataContext`. `INotifyPropertyChanged` raises `PropertyChanged(name)` so bindings refresh;
a wrong name, or not raising it, means the UI never updates. `ObservableCollection<T>` raises `CollectionChanged`
for adds and removes, but not for changes to an item's properties; replacing the whole collection needs
`PropertyChanged` on the property that holds it. Binding errors are silent: check the Output window or the XAML
Binding Failures window. See 11-01.

**G2. Dependency properties.** They make binding, styles, animation, inheritance down the element tree, default
values and change callbacks possible, with a precedence system (local value, style, template, inherited). They are
stored sparsely, which saves memory for controls with hundreds of properties. Any property on your own control
that should accept a binding must be one. See 11-04.

**G3. The UI thread.** WPF objects belong to the dispatcher thread that created them. From a background thread,
marshal back with `Dispatcher.InvokeAsync`, by awaiting from the UI context, or through `IProgress<T>` created on
the UI thread (`Progress<T>` captures the context). Do not flood the dispatcher with thousands of progress
updates; throttle them. See 11-02.

**G4. Async commands.** `ICommand.Execute` returns `void`, so an async lambda in it becomes `async void`. Use an
async command (`AsyncRelayCommand` in CommunityToolkit.Mvvm, or your own) that awaits a `Task`, catches and
surfaces exceptions, disables `CanExecute` while running (no double start, see 04-02), and supports cancellation.

**G5. WPF → MAUI.** Navigation: WPF's `Frame` or custom window management becomes Shell, with routes and query
parameters (`IQueryAttributable`, `[QueryProperty]`), and navigation is async (12-01). Lifecycle: a desktop app
runs until it is closed; a mobile OS can suspend or kill your process, so persist state early (12-03). Threading:
`MainThread.BeginInvokeOnMainThread` or the dispatcher; on Android and iOS, UI updates from background threads
fail (12-02). Controls: `CollectionView` instead of `ItemsControl` or `ListView`, `BindableProperty` instead of
`DependencyProperty`, handlers instead of control templates for platform customisation. Platform code: the
`Platforms/` folders, partial classes, runtime permissions. DI is built in.

**G6. Memory leaks.** The symptom: memory grows with every operation. Tools: Visual Studio's memory snapshots
(compare two, look at object-count differences and paths to root), dotMemory, PerfView, `dotnet-gcdump`. Common
causes: event subscriptions to long-lived objects, static caches, timers (a `DispatcherTimer` keeps its target
alive), WPF bindings to sources without `INotifyPropertyChanged`, undisposed resources, unfrozen images. See 11-03.

**G7. Desktop deployment.** MSI: Windows Installer, per-machine, needs admin, fits enterprise tooling. MSIX:
modern packaging with clean install and uninstall, and updates through App Installer. ClickOnce: per-user,
auto-updating .NET apps (Sage 200's desktop client is installed this way). There are also Squirrel- or
Velopack-style updaters. *For an SDK:* it ships inside the host's installer, so it updates when the host updates.

## H. SDKs and .NET Standard

**H1. .NET Standard 2.0.** A specification of APIs that several .NET implementations support: .NET Framework
4.6.1+ (4.7.2+ in practice), .NET Core 2.0+ and .NET 5+, Mono, Xamarin and Unity. An SDK targets it when one
binary must load into both .NET Framework hosts, such as Sage's desktop products, and modern .NET apps. It is not
the default for new code; use it only when compatibility requires it.

**H2. What you lose, and how to get some back.** Lost: newer APIs (span-based overloads, `IAsyncEnumerable`,
`ValueTask`, `Parallel.ForEachAsync`, `HashCode`, `TimeProvider`, built-in `System.Text.Json`,
`ArgumentNullException.ThrowIfNull`) and runtime features such as default interface members. Get back through
packages: `System.Memory`, `Microsoft.Bcl.AsyncInterfaces`, `System.Threading.Tasks.Extensions`,
`System.Text.Json`, `Microsoft.Bcl.TimeProvider`, `Microsoft.Bcl.HashCode`. Compiler features such as records and
`init` need small polyfills (`IsExternalInit`, the nullable attributes), and the C# version can be newer than the
target framework for syntax-only features. See 09-04.

**H3. Multi-targeting.** With `<TargetFrameworks>netstandard2.0;net10.0</TargetFrameworks>`, modern hosts get
the better implementation (span APIs, built-in JSON, no polyfill packages) while .NET Framework still works;
`#if` handles the differences. The cost is two builds to test, and conditional code.

**H4. Breaking changes.** *Binary* (existing apps fail at runtime with `MissingMethodException` or
`TypeLoadException` until recompiled): removing or renaming a public member, changing a parameter or return type,
adding a parameter (even an optional one, because the signature changes), adding an abstract member or an
interface member, changing a struct to a class. Changing a `const` value is worse: nothing fails, but old apps keep
the old value. *Source* (breaks on recompile): a new overload that makes existing calls ambiguous, renamed
parameters (breaks named arguments), a new member that clashes with an extension method. *Behavioural:* same
signature, different result, e.g. it now throws where it used to return null. Tools: `PublicApiAnalyzers`, and
package validation against a baseline version. See 09-05.

**H5. Dependency conflicts.** *.NET Framework:* one version of an assembly per AppDomain (in the default load
context), and the host's `app.config` binding redirects decide which. If your SDK needs a newer version than the
host ships, and there is no redirect, you get `FileLoadException` or `MissingMethodException` at runtime. If the
host redirects to an older version, your SDK may call a method that does not exist. Strong naming makes version
matching strict. **The SDK cannot edit the host's config.** Mitigations: minimal dependencies, the lowest version
that works, avoiding libraries hosts often ship in other versions (Newtonsoft.Json is the classic), or embedding
a private copy (licence permitting). *Modern .NET:* the app's `deps.json` resolves one version (the highest
requested); `AssemblyLoadContext` can isolate plugins.

**H6. Versions.** `AssemblyVersion` is what the runtime uses for binding (strictly, for strong-named assemblies
on .NET Framework). Changing it forces binding redirects, so many libraries keep it at `Major.0.0.0`.
`FileVersion` is informational: file properties, and installers comparing files. The package version (SemVer) and
`InformationalVersion` are what people and package managers see, and can carry prerelease labels or a commit hash.

**H7. Public vs internal.** Public: the entry point (a client or facade), options, result and progress types,
exception types, and interfaces the host implements (extension points such as source readers or credential
providers). Internal: HTTP DTOs, retry logic, serializers, the state storage format, transports. Every public type
is a promise you keep across versions: start small, seal classes that are not designed for inheritance, and use
`InternalsVisibleTo` for tests. See 09-01 and 09-02.

**H8. Shipping an SDK others can use without you.** XML docs on every public member (they appear in
IntelliSense). A README with a quick start and a working sample host app (WPF and .NET Framework). A changelog and
a deprecation policy (`[Obsolete]` with a message and the version it will be removed in). SemVer. A NuGet package
with symbols and SourceLink so people can debug into it. Clear exception messages and logs with correlation IDs.
A compatibility matrix.

## I. Azure

**I1. A 20 GB file to Blob Storage.** Use a block blob: upload the file as blocks (Put Block; up to 50,000 blocks,
each up to 4,000 MiB in current service versions), in parallel with retries, then commit them with Put Block List.
Uncommitted blocks make resuming possible. `Azure.Storage.Blobs`' `UploadAsync` does this for you, configured
through `StorageTransferOptions` (concurrency, transfer sizes). Validate with MD5 or CRC64. This is the same idea
as the resumable upload in 07-02.

**I2. SAS.** A Shared Access Signature is a signed URL that grants specific permissions (e.g. write to one blob)
for a limited time, without sharing the account key. It should be short-lived because anyone holding it can use
it, and a SAS signed with the account key cannot be revoked individually. A user delegation SAS is signed with
Entra ID credentials instead: preferred, revocable, auditable. The common pattern: the API authenticates the
desktop client and issues a short-lived, write-only SAS for exactly one upload target, and the client uploads
straight to Blob Storage. See 16-06.

**I3. Managed identity.** It gives Azure-hosted resources (App Service, Functions, VMs) an identity without
secrets. A desktop app on a customer's machine is not an Azure resource, so it cannot have one. It signs the user
in (OAuth), and the *backend* uses managed identity to reach Storage, Key Vault or the database.

**I4. Queues for long-running work.** Restoring a company or validating a 20 GB backup should not hold an HTTP
request open. The API accepts the job (202), puts a message on a queue, a worker processes it, and the client polls
for status (08-02). Queues decouple the parts, absorb spikes and allow retries. Storage Queues: simple, cheap,
at-least-once delivery, 64 KB messages. Service Bus: sessions (ordering), dead-lettering, duplicate detection,
transactions, topics. Durable Functions for multi-step workflows with state.

**I5. Secrets.** Backend: Azure Key Vault, reached through managed identity; no secrets in config files.
Desktop: ship no secrets at all. Protect per-user tokens with DPAPI (`ProtectedData`, CurrentUser scope), the
Windows Credential Manager, or MSAL's token cache. Never in plain text, and never in logs.

**I6. Azure Virtual Desktop.** A Microsoft service that runs Windows desktops and apps on Azure virtual machines
and streams them to users through the Windows App or Remote Desktop clients. It appears to be how Sage 50's
hosted cloud edition is delivered.

## J. About you

No model answers: these must be your own. Use `prep/story-bank.md`.
