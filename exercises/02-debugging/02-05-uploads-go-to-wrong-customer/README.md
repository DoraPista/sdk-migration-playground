# Exercise 02-05 – Uploads Go to the Wrong Customer

Difficulty: Medium
Estimated Time: 25 minutes

## Skills

- dependency injection lifetimes
- per-operation state
- concurrency in desktop apps

## Scenario

The desktop app was originally built for one customer per installation. A new version lets
migration consultants run migrations for **several customers from one app instance**, one after another
or at the same time.

Since the release, two serious incidents:

1. A consultant migrated **Northwind**, then **Contoso**. Contoso's files ended up in Northwind's cloud destination.
2. Two migrations started at the same time. Some of Fabrikam's files were uploaded *tagged with Contoso's customer ID*.

The app runs every migration job in its own DI scope (see `JobHost`). The service registrations
are in `ServiceCollectionExtensions.AddMigrationKit`.

## Your Task

Find the cause of both incidents and fix it.

## Constraints

- Keep the public API of `MigrationJob` and `JobHost`.
- The destination lookup is slow (it calls a directory service), so don't look it up once per file.

## Acceptance Criteria

- Each job uploads to its own customer's destination, with its own customer ID, whether jobs run one after another or at the same time.

## How to Run

```bash
dotnet test exercises/02-debugging/02-05-uploads-go-to-wrong-customer/tests
```

## When You're Done

All tests pass, and you can explain how you would stop this class of bug from coming back.
