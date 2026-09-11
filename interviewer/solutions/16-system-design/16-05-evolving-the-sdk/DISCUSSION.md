# 16-05 Evolving the SDK Without Breaking Desktop Clients – Discussion Notes

**Type:** system design · **Time:** 20 min

## Hints

1. "Define what you mean by breaking before you judge any of the four changes."
2. "Which breaks appear only at runtime, on a laptop you cannot update?"
3. "Add rather than change, with [Obsolete] for a release; pin the platform api-version; version the checkpoint format; and make an API approval test fail the build."

Related exercises: 09-02 (tolerant reader / api-version), 09-04 (.NET Standard 2.0 + net48 host),
05-02 (the server changed underneath the client).

## 1. The three kinds of break

A candidate who only knows "don't change public signatures" is mid-level at best. The distinctions:

| Kind | Example | Who notices |
|---|---|---|
| **Source** | Renaming a method; adding a parameter without a default; adding an interface member | Whoever recompiles. The customer integration that builds against the DLL |
| **Binary (ABI)** | Adding a parameter *with* a default (the call site bakes in the argument), changing a return type, `struct` → `class`, adding an overload that changes resolution, changing a const | Nobody at compile time. The v1.1 laptop gets `MissingMethodException` at runtime |
| **Behavioural** | Retries now take 30 s instead of 3; progress reports bytes instead of files; an error that was thrown is now swallowed; the default chunk size changes | Nobody until a customer complains. **The most dangerous category** |

The question at the end of the exercise ("compiles fine, ABI-safe, still breaks a customer") is asking for a
behavioural break. Good answers: changing the meaning of a progress callback, making a previously
fire-and-forget call throw, changing the default timeout, changing a file name in the checkpoint format.

## 2. The four changes in the scenario

- **Resumable uploads** — additive if it is a new method/option and the default is unchanged. Making it the
  default is a behavioural break (new files on disk, different server calls) and belongs in a minor with a
  release note, or behind an opt-in for one release.
- **Progress reporting change** — behavioural, and the worst of the four. Add the new shape alongside the old
  (`IProgress<MigrationProgress>` next to the existing event), mark the old one `[Obsolete]`, remove in the next
  major. Never silently change what a number means.
- **The two renames** — source-breaking and binary-breaking. Do it by adding the new name and keeping the old
  one as `[Obsolete]` forwarding to it. They cost almost nothing to keep for a year.
- **`api-version=2026-03-01`** — pin it in the SDK, do not follow the server automatically (05-02). Each SDK
  version declares the platform version it speaks; the platform must support N-2 versions or the laptops break.

## 3. A policy that fits in the README

- **SemVer**, applied honestly, with the *contract* defined: public API, the checkpoint file format, the
  platform API version, and the documented behaviour of retries and progress.
- Patch: bug fixes with no behaviour change a caller could depend on. Minor: additive, `[Obsolete]` allowed.
  Major: removals; at most once or twice a year; migration guide required.
- Support N-1 minor for 12 months; state it, because the MSI story means old versions live for a long time.
- `[Obsolete]` for at least one minor before removal; `[EditorBrowsable(Never)]` for things kept only for ABI.
- Every release: a changelog entry per public change, and an explicit **behavioural changes** section.
- Tooling beats discipline: a public-API approval test (`PublicApiGenerator`/`Microsoft.CodeAnalysis.PublicApiAnalyzers`)
  so an accidental signature change fails the build (09-03 does exactly this for assembly references).

## 4. Two SDK versions in one environment

- Strong-named, versioned assembly; no assumption of a single instance.
- Nothing global and mutable (statics, fixed file names, registry keys, a checkpoint path without a version).
- The checkpoint format carries a `formatVersion`; a newer SDK reads older checkpoints, an older SDK refuses
  a newer one **with a clear message** instead of misreading it.
- For the net48 integration: `netstandard2.0` target, watch the transitive package graph (this is where
  binding redirects and `System.Text.Json` versions bite — 09-04).

## 5. "What would you have done in v1.0?"

The reflective question; strong candidates have real answers:

- Fewer public types; `internal` by default, opened deliberately.
- Options objects instead of parameters, so adding a setting is never a break.
- No public fields, no public mutable collections (15-01), sealed classes unless extension is designed.
- Interfaces owned by the *consumer* where possible, since adding a member to a published interface is a break
  (or plan for default interface members, which the net48 target cannot use).
- A versioned checkpoint format and an explicit api-version from day one.
- An API approval test from the first commit.

## Follow-ups

- "A v1.1 laptop talks to the new platform version. What happens?" (Design for it: version negotiation, a clear error, and a supported window.)
- "You must ship a security fix to the v1.1 laptops." (Patch on a maintenance branch; this is why you keep one.)
- "Is adding an optional parameter safe?" (Source-compatible, **not** binary-compatible — a good discriminator.)
- "Is adding a field to a JSON response breaking?" (Not if everyone is a tolerant reader — 09-02 — which is why the SDK must be one.)

## Level indicators

| Level | Indicators |
|---|---|
| Weak | "Use SemVer" with no definition of the contract; thinks only about compile errors |
| Solid mid-level | Distinguishes source vs binary; obsolete-then-remove; pins the api-version; keeps net48 working |
| Strong | Names behavioural breaks and gives a real example; versions the checkpoint format; automates API approval |
| Senior | Treats compatibility as a product commitment: support windows, maintenance branches, deprecation communication, and what they would design differently to make change cheap |
