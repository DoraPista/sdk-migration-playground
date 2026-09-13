# Quick-fire Questions

For the Brillio interview, and for the first minutes of any technical round. Answer each one **out loud** in
30–60 seconds, then check yourself against `interviewer/prep/quick-fire-answers.md`. Mark the ones you
hesitated on and repeat them two days later.

A textbook answer scores a 3. The same answer with *"for example, in my last project…"* scores a 5.

## A. C# and .NET fundamentals

- **A1.** Value types vs reference types: where does each live, and what happens when you pass one to a method?
- **A2.** What does a `record` give you over a `class`? When would you use a `record struct`?
- **A3.** Interface or abstract class: how do you choose?
- **A4.** Overloading vs overriding. What do `virtual`, `override`, `new` and `sealed` do?
- **A5.** What is boxing, and where does it sneak in?
- **A6.** `IEnumerable<T>` vs `IQueryable<T>`. What is deferred execution, and how can it bite you?
- **A7.** When do you implement `IDisposable`, and what does `using` compile to? When `IAsyncDisposable`?
- **A8.** How does the garbage collector work? What is the Large Object Heap, and why does it matter for file transfer code?
- **A9.** `throw;` vs `throw ex;` vs wrapping the exception in a new one.
- **A10.** Delegates, events, `Func` and `Action`. How do events cause memory leaks?
- **A11.** What do nullable reference types actually guarantee?
- **A12.** What are generic constraints for? What is covariance (`IEnumerable<out T>`)?
- **A13.** Why are strings immutable, and when do you need a `StringBuilder`?
- **A14.** `const` vs `readonly` vs `static readonly`. Why is a public `const` risky in an SDK?

## B. Object-oriented design

- **B1.** The four pillars of OOP, each with an example from your own code.
- **B2.** SOLID: pick two principles and show where you applied or broke them.
- **B3.** Composition vs inheritance.
- **B4.** Which patterns have you used (MVVM, repository, factory, strategy, observer, decorator)? When did one make the code worse?
- **B5.** What makes a class hard to unit test?

## C. Async, threading and concurrency

- **C1.** What does `await` actually do? What is a `SynchronizationContext`?
- **C2.** Why can `.Result` or `.Wait()` freeze a WPF app? Why do libraries use `ConfigureAwait(false)`?
- **C3.** When is `async void` acceptable?
- **C4.** When is `Task.Run` right, and when is it a warning sign, especially in library code?
- **C5.** You `await Task.WhenAll(...)` and one task fails. What do you see?
- **C6.** How does cancellation work in .NET? What does a method that accepts a `CancellationToken` owe its caller?
- **C7.** `lock` vs `SemaphoreSlim` vs `Interlocked` vs concurrent collections.
- **C8.** What is a race condition, and what is a deadlock? Give one of each that you have seen.
- **C9.** `Task` vs `ValueTask`.
- **C10.** How would you run 500 uploads with at most 8 at a time? Give two ways.

## D. Dependency injection and app structure

- **D1.** Singleton, scoped, transient. What is a captive dependency?
- **D2.** How do you use DI in a WPF app? In a MAUI app?
- **D3.** Should an SDK force a DI container on the apps that use it? How would you support both kinds of app?
- **D4.** Logging and configuration in a library: what should an SDK take from the host app?

## E. Data and SQL

- **E1.** INNER vs LEFT JOIN. GROUP BY with HAVING.
- **E2.** Write a query: the second-highest invoice total for each customer. Talk through it.
- **E3.** Clustered vs non-clustered indexes. When does an index hurt?
- **E4.** Transactions and isolation levels. What is a dirty read?
- **E5.** Entity Framework: tracking vs no-tracking, N+1 queries, migrations.
- **E6.** Why must money be `decimal` and not `double`?

## F. Web APIs and HTTP

- **F1.** What do 200, 201, 202, 204, 400, 401, 403, 404, 409, 422, 429, 500, 502 and 503 mean to a client?
- **F2.** Which HTTP methods are idempotent, and why does that matter for retries?
- **F3.** How should `HttpClient` be created and reused? What goes wrong otherwise?
- **F4.** REST vs RPC-style APIs. How would you version an API?
- **F5.** OAuth2 client credentials vs authorization code with PKCE. Which fits a desktop app, and why?
- **F6.** How do you make an API client resilient?

## G. Desktop: WPF and MAUI

- **G1.** How does WPF data binding work? What do `INotifyPropertyChanged` and `ObservableCollection<T>` do?
- **G2.** Why do dependency properties exist?
- **G3.** Why can't a background thread update a control, and what do you do instead?
- **G4.** `ICommand` is synchronous. How do you implement an async command without losing exceptions?
- **G5.** What changes when moving from WPF to MAUI: navigation, lifecycle, threading, controls, platform code?
- **G6.** How do you find a memory leak in a desktop app?
- **G7.** How are desktop apps deployed and updated (MSI, MSIX, ClickOnce)?

## H. SDKs and .NET Standard

- **H1.** What is .NET Standard 2.0, what can use it, and why would an SDK still target it in 2026?
- **H2.** What do you lose by targeting `netstandard2.0`, and how do you get some of it back?
- **H3.** When would you multi-target, e.g. `netstandard2.0;net10.0`?
- **H4.** Binary vs source breaking changes: give three examples of each.
- **H5.** Your SDK and the host app need different versions of the same library. What happens on .NET Framework, and on modern .NET?
- **H6.** `AssemblyVersion` vs `FileVersion` vs package version.
- **H7.** What should be public in an SDK, and what should stay internal?
- **H8.** How would you ship and document an SDK so another team can integrate it without having to call you?

## I. Azure (nice to have)

- **I1.** How would you upload a 20 GB file to Blob Storage?
- **I2.** What is a SAS token? Why should it be short-lived? What is a user delegation SAS?
- **I3.** Why doesn't managed identity help a desktop client directly?
- **I4.** A queue vs a direct API call for long-running work. Service Bus vs Storage Queues.
- **I5.** Where do secrets live on the backend? On the desktop?
- **I6.** What is Azure Virtual Desktop, in one sentence?

## J. About you (Brillio will ask these)

- Walk me through your CV in two minutes.
- Why consulting? Why Brillio?
- What is the most complex thing you have built? What is the hardest bug you have fixed?
- Tell me about working with a client or stakeholder who disagreed with you.
- How do you keep people informed about progress and blockers when working remotely?

These are all in [story-bank.md](story-bank.md).
