# Mock Data

Realistic customer and migration data used by the exercises and tests. Everything here is
deterministic: `tools/GenerateMockData.cs` regenerates it byte-for-byte.

```bash
dotnet run shared/MockData/tools/GenerateMockData.cs -- shared/MockData
# Optional: also write a large real file to shared/MockData/generated/ (git-ignored)
dotnet run shared/MockData/tools/GenerateMockData.cs -- shared/MockData --large-files 2048
```

## Layout

| Path | Contents |
|---|---|
| `customers.json` | 4 customers |
| `projects.json` | 8 projects, nested up to 3 levels (`parentId`) |
| `files.json` | Manifest of the **normal** dataset (19 entries). Paths are relative to `files/` |
| `files/` | Real file content for the normal dataset |
| `migrations.json` | Migrations in different states |
| `migration-state.json` | Local checkpoint of a migration the desktop app was killed in the middle of |
| `datasets/large/` | 150 projects / 5,000 file entries (metadata only; includes several 100 GB entries) |
| `datasets/malformed/` | Invalid data: duplicate IDs, a project cycle, orphans, bad hashes, path traversal, bad metadata, and a truncated JSON file |
| `datasets/results/` | Per-file upload results as reported by a legacy agent |

## Things in the normal dataset

The normal dataset is "normal" the way real customer data is:

- a zero-byte file
- file names with non-ASCII characters (German, Japanese, Spanish, emoji)
- one file whose content does not match the manifest hash
- one manifest entry whose file does not exist on disk
- files from a few hundred bytes to ~1.5 MB

Large files are **not** committed. Tests create them on demand with `Gym.TestUtilities.TestFiles`.
