# Exercise 07-04 – Real Customer Data

Difficulty: Medium
Estimated Time: 25 minutes

## Skills

- edge cases in file data
- HTTP header encoding
- data integrity checks
- per-item error handling

## Scenario

`DatasetUploader` migrates a customer export: a manifest (`files.json`) plus the files it describes.
It passed QA with the QA team's sample export.

The first real customer export, Northwind's, is in `shared/MockData` (`files.json` and the `files/` folder).
The migration crashes part-way through, and support can't tell which files made it.

Northwind's IT lead also asks:

> "Our export tool writes the SHA-256 of every file into the manifest. Do you check it? We had a
> storage controller fault last year and we're not sure every file on that share is still intact."

The platform shows files with their **folder path** (`documents/specification.pdf`), and file names are sent
as described in `shared/MockServer/API.md`.

## Your Task

Make `DatasetUploader` cope with Northwind's real data and report exactly what happened to each file.

## Constraints

- Keep the public API (`DatasetUploader`, `DatasetReport`, `DatasetProblem`, `DatasetProblemKind`).
- A problem with one file must not stop the others.
- A file whose content doesn't match the manifest must not be uploaded.

## Acceptance Criteria

- Every intact file in the export arrives with its folder path and name exactly as in the manifest, including empty files and non-English names.
- Missing files and files that don't match the manifest are reported, not uploaded.

## How to Run

```bash
dotnet test exercises/07-file-transfer/07-04-customer-dataset-edge-cases/tests
```

## When You're Done

All tests pass, and you can list every kind of "surprising" data in Northwind's export.
