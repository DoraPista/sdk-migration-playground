# 09-05 Shipping Version 2 Without Breaking Anyone – Interviewer Notes

**Type:** expert discussion (answer key) · **Time:** 20 min

## Hints

1. "Before judging any change, sort them into three buckets: does not compile, compiles but fails at runtime, compiles and runs but behaves differently."
2. "Which values does the compiler copy into the *caller's* assembly?"
3. "Optional-parameter defaults, consts and enum numbers are baked into call sites. Changing them is a silent break for an app that is never recompiled."

Key distinction to establish early:

- **Binary breaking**: an already-compiled application fails (usually `MissingMethodException`, `TypeLoadException`) when the new DLL is dropped in. The big customer in the scenario does exactly this.
- **Source breaking**: partners' code no longer compiles when they upgrade and rebuild.
- **Behavioural**: compiles and runs, different outcome.

## Answer key

| # | Change | Binary | Source | Behaviour | Notes / what to do instead |
|---|---|---|---|---|---|
| 1 | Add members to **`IMigrationClient`** | **Yes** for implementers | **Yes** for implementers | – | Any type implementing the interface (partner fakes, test doubles) breaks. .NET Standard 2.0 has **no default interface methods** (they need .NET Core 3+), so that escape hatch isn't available. Options: put the members on the *class* only; ship `IMigrationClient2`; or document the interface as "not for external implementation" and accept the risk in a major version |
| 2 | Add an **optional parameter** | **Yes** | No (recompile works) | – | Optional-argument values are baked into the **call site**, and the method signature changes, so old compiled callers get `MissingMethodException`. Add an **overload** instead (see #14) |
| 3 | `Task<T>` → `ValueTask<T>` | **Yes** | **Yes** | – | The return type is part of the signature; callers with `Task<MigrationHandle> t = ...` stop compiling. Not worth it for one call per migration. Micro-optimisation on a cold path |
| 4 | Add a property to `MigrationProgress` | No | No | Minor | Safe, because it's a plain class with settable properties. *If it were a positional `record`*, adding a positional parameter would change the constructor/`Deconstruct` and equality: binary and source breaking. Worth asking |
| 5 | **Rename** an enum member | **Yes** | **Yes** | – | Also breaks anything persisting the name as a string (checkpoints, telemetry, the platform's API). Add the new name and `[Obsolete]` the old one, or leave it alone |
| 6 | **Insert** an enum member, renumbering | **Yes** (silently!) | No | **Yes** | Enum constants are **inlined into consumer assemblies**: old code keeps the old number, so `Validating` now means `Scanning`. The worst kind: no exception, wrong behaviour, and old persisted checkpoints are misread. Append with a new value instead |
| 7 | **Append** an enum member | No | No | **Yes** for exhaustive switches | Allowed, and a contract question: does the SDK promise no new values? Document it and make callers handle `default` (see 05-02). Consider "extensible enum" structs for values that grow often |
| 8 | `sealed class` | **Yes** for derived types | **Yes** | – | Only affects anyone deriving. Fine in a major version; check whether partners do. Sealing is good hygiene, but it's a break |
| 9 | Public **field → property** | **Yes** | Mostly no (breaks `ref`/`out` use and some reflection) | – | Field access compiles to `ldfld`, property to `callvirt`. Old binaries break. Lesson: never ship public fields (see 09-02) |
| 10 | `IEnumerable<T>` → `IReadOnlyList<T>` **parameter** | **Yes** | **Yes** for callers passing a lazy sequence | – | Add an overload, or keep `IEnumerable<T>` and materialise inside (what the constructor already does) |
| 11 | Change a **default value** 4 → 16 | No | No | **Yes** | Customers behind proxies get connection resets (see 03-01); the platform sees 4× the concurrency from every desktop. Defaults are part of the contract. Change in a major version, in release notes, ideally adaptively |
| 12 | **Move a type** to another namespace | **Yes** | **Yes** | – | Type identity includes the namespace. `[TypeForwardedTo]` only helps when a type moves to a *different assembly*, not for a rename inside the same one. Not worth it for tidiness |
| 13 | `[Obsolete]` a method | No | Warning (error with `TreatWarningsAsErrors`) | – | The right way to deprecate. Give the replacement in the message, and a timeline (obsolete in 2.x, removed in 3.0) |
| 14 | Add an **overload** | No | Usually no | – | The safe way to extend. Watch for ambiguity with optional parameters and `null` literals |

## Shipping plan (what a good answer contains)

- **Semantic versioning of the assembly and the package.** Breaking changes (1, 2, 3, 5, 6, 8, 9, 10, 11, 12) ⇒ **2.0.0**.
  Ship 1.5 with only the safe changes (4, 7, 13, 14) so customers get value without a migration.
- **Assembly versioning strategy**: `AssemblyVersion` fixed per major (`2.0.0.0`) with `FileVersion`/`InformationalVersion` moving,
  so patch updates can be dropped in. Binding redirects / `assemblyBinding` for .NET Framework hosts.
- **Deprecation policy**: obsolete in the current major, remove in the next; document dates.
- **Tooling so it can't happen by accident**:
  - `Microsoft.CodeAnalysis.PublicApiAnalyzers` with `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt` (every public API change becomes a reviewable diff), or
  - `EnablePackageValidation` + `PackageValidationBaselineVersion` (ApiCompat runs on every build against the last shipped package),
  - a compatibility test project compiled against 1.4 and run against 2.0.
- **Communication**: release notes with a migration table, and a real upgrade guide for the partners.
- Optional: ship **both** majors for a transition period (`MigrationKit` 1.x maintenance branch).

## Follow-up questions

- "The big customer drops DLLs in without recompiling. Should we support that?" (It's how the world works; it's also why `AssemblyVersion` and binary compatibility matter. Or ship an installer that updates everything.)
- "How do you find out whether any partner implements `IMigrationClient`?" (Ask; telemetry can't see it. Design for the worst case.)
- "What does `[Obsolete(error: true)]` do to a partner build?" (Compile error: a removal in disguise.)
- "Is adding an interface *implementation* to an existing public class breaking?" (No, unless it creates ambiguity for extension methods / overload resolution.)

## Scoring notes

| Level | Indicators |
|---|---|
| Weak | "Just bump the version"; doesn't know optional parameters or enum values are baked into callers; no distinction between binary and source |
| Solid mid-level | Gets most classifications right, especially 2, 5, 6, 9; proposes overloads and `[Obsolete]`; understands semver |
| Strong | Explains *why* (call sites, constant inlining, signature identity); spots the silent behaviour break in 6 and the default-value break in 11; suggests API-approval tooling |
| Senior | Has a complete release plan: dual majors, assembly-version policy, ApiCompat in CI, partner communication, and the "what is our compatibility promise" conversation |
