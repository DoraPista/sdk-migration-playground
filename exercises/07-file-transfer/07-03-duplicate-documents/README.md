# Exercise 07-03 – Duplicate Documents

Difficulty: Medium
Estimated Time: 25 minutes

## Skills

- idempotency
- ambiguous failures
- designing identifiers

## Scenario

Customers of a document-management system (DMS) migrate their documents with the SDK. Each document has a
stable ID in the DMS (`DOC-…`).

After migrations over hotel and train Wi-Fi, customers find **duplicates** in the portal: the same tender
document two or three times. Some duplicates appear even after the app crashed and the user simply ran
the migration again.

The platform supports idempotent uploads (`Idempotency-Key`, see `shared/MockServer/API.md`), and
`DocumentUploader` already sends one.

## Your Task

Find out why duplicates still happen and fix `DocumentUploader`.

## Constraints

- Keep the public API.
- Two different documents may have the same file name (e.g. `Report.pdf` in two projects).
- If a user edits a document in the DMS and migrates again, the new version must be uploaded.

## Acceptance Criteria

- A document is stored once, even if a response is lost or the app is restarted and the migration runs again.
- A request that never reached the server is still delivered.
- Different documents with the same name are both kept, and an edited document is uploaded again.

## How to Run

```bash
dotnet test exercises/07-file-transfer/07-03-duplicate-documents/tests
```

## When You're Done

All tests pass, and you can explain what the idempotency key identifies.
