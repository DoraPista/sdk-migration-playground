# 07-04 Real Customer Data – Interviewer Notes

**Type:** debugging with real data · **Time:** 25 min · **Solution code:** `code/src/DatasetUploader.cs`
**Uses:** `shared/MockData/files.json` + `shared/MockData/files/`

## What's in Northwind's export (and what the code does with it)

| Data | Entry | Current behaviour |
|---|---|---|
| Zero-byte file | F-0009 `documents/empty-notes.txt` | `stream.Length * 100 / entry.SizeBytes` → **DivideByZeroException**, and the whole run dies here (that's the crash support sees) |
| Non-ASCII names | F-0010 `Übersicht…`, F-0011 `設計図…`, F-0012 `Résumé – José Núñez`, F-0013 `naïve café 😀` | Raw name in a header → `HttpRequestException: Request headers must contain only ASCII characters` |
| Folder structure | all | `Path.GetFileName` drops `documents/`, `images/`, so the portal loses folders and same-named files in different folders collide |
| Damaged file | F-0014 `images/site-photo-017.jpg` (content ≠ manifest hash) | The hash is recomputed from disk and sent, so the server happily stores the damaged file: **silent corruption** |
| Missing file | F-0015 `documents/missing-appendix.pdf` | `FileNotFoundException` aborts the run |
| Report | – | Always `Problems = []`; support can't tell what happened |
| Also | `?projectId=` not URL-encoded; `/` separators on Windows are fine by luck | |

## Hints

1. "Run it against the real export. Where does it stop, and why that file?"
2. "What characters may an HTTP header contain? What does the API contract say about file names?"
3. "Check each file against the manifest before uploading; handle problems per file; report them."

## Intended solution

Per-entry `try`/report; exists check; hash compare against the manifest **before** upload; percent-encoded relative path; no division by zero (empty = 100%).

### Discussion points

- **Why check the manifest hash?** The customer asked. It also turns "we migrated your corrupted file" into "we found 1 damaged file"; that's evidence.
- **Unicode normalisation.** `Résumé` can be NFC (`é` = U+00E9) or NFD (`e` + U+0301). macOS file systems historically returned NFD. Should the SDK normalise names to NFC? (Ask. It prevents "duplicate" names in the portal that look identical.)
- **Path traversal.** A malicious or broken manifest could contain `../../Windows/...` (see the malformed dataset). The solution should reject paths that escape `datasetRoot`. That's a senior-level catch (10-02 covers it as a validation rule).
- **Case sensitivity.** `Images/` vs `images/` on case-insensitive NTFS vs case-sensitive destinations.

## Common mistakes

- `HttpUtility.UrlEncode`: encodes spaces as `+`, which doesn't round-trip with `Uri.UnescapeDataString` (the server would store `+`).
- Using `TryAddWithoutValidation` with the raw name (still non-ASCII on the wire).
- Uploading the damaged file and "just logging" the mismatch.
- `catch (Exception)` per file, which also swallows cancellation (use filters).

## Follow-ups

- "Northwind's next export has 3 million entries. What changes?" (Stream the manifest with `DeserializeAsyncEnumerable`; parallelism; report to a file.)
- "The manifest has no hashes for some files. Now what?" (Upload with a locally computed hash, mark as "unverified" in the report.)
- "Should one damaged file block completing the migration?" (Product decision: report and let the customer decide.)

## Scoring notes

| Level | Indicators |
|---|---|
| Weak | Fixes crashes one at a time with try/catch-all; uploads the damaged file; doesn't read the API contract |
| Solid mid-level | Per-file handling, missing/mismatch reported, encoded relative paths, empty files OK |
| Strong | Explains header ASCII rules, `+` vs `%20`, why checking the manifest hash matters |
| Senior | Unicode normalisation, path traversal, case sensitivity, scale (streaming manifests), report design for customers |
