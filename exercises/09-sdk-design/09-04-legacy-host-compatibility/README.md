# Exercise 09-04 – The Customer's .NET Framework 4.8 Application

Difficulty: Medium
Estimated Time: 25 minutes

## Skills

- target frameworks and .NET Standard
- API availability across runtimes
- polyfills and packages
- keeping behaviour while changing platform

## Scenario

Our biggest customer embeds `MigrationKit.Transfer` in their own application. That application is
**.NET Framework 4.8** and will be for at least two more years: it hosts a 15-year-old CAD add-in
that will never move.

Their build fails with:

```
error NU1201: Project MigrationKit.Transfer is not compatible with net48 (.NETFramework,Version=v4.8).
              Project MigrationKit.Transfer supports: net10.0 (.NETCoreApp,Version=v10.0)
```

The team decided: **this library targets `netstandard2.0`**, single target, so that the .NET Framework host,
our .NET 10 desktop app and the MAUI app can all use it.

A minimal version of the customer's host is in [`samples/LegacyHost`](samples/LegacyHost) (it is not part of the
solution). Today it doesn't build; when you're done, it should.

## Your Task

Retarget the library to `netstandard2.0` and make it compile, without changing what it does.

## Constraints

- Keep the public API and the behaviour: the tests must pass unchanged.
- Keep the code readable. Wholesale rewrites of working logic are not the point.
- You may add NuGet packages and small internal helpers.

## Acceptance Criteria

- The library targets .NET Standard 2.0.
- All existing behaviour tests still pass.
- `dotnet build exercises/09-sdk-design/09-04-legacy-host-compatibility/samples/LegacyHost` succeeds.

## How to Run

```bash
dotnet test exercises/09-sdk-design/09-04-legacy-host-compatibility/tests
dotnet build exercises/09-sdk-design/09-04-legacy-host-compatibility/samples/LegacyHost
```

## When You're Done

All tests pass, and you can say what .NET Standard 2.0 costs us here, and when you would *not* accept that cost.
