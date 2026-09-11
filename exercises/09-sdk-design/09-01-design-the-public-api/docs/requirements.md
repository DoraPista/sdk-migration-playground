# MigrationKit – Product Requirements (v1)

## Must have

1. **Start a migration** for a customer from a local or network folder, or from an explicit list of files
   provided by the host application.
2. **Progress**: the host shows the current stage (validating, uploading, verifying), files done / total,
   bytes done / total, and the file currently being uploaded. The WPF app updates a progress bar and a
   file list; the MAUI app shows a compact summary.
3. **Cancel** a running migration. Cancelling must not leave the platform in an inconsistent state.
4. **Result**: when a migration ends, the host must know whether it succeeded, failed, was cancelled, or
   partly succeeded, and which files failed and why (in a form suitable to show to the user and to send to support).
5. **Resume**: if the app is closed or crashes, the next start of the app can resume unfinished migrations.
6. **Several migrations** can run at the same time (a consultant migrating three customers). Max 3 concurrently.
7. **Logging** goes into the host application's existing logging (the WPF app uses `Microsoft.Extensions.Logging`
   with Serilog; the MAUI app uses `Microsoft.Extensions.Logging`).
8. **Configuration**: platform URL, concurrency, retry settings. Credentials come from the host (the WPF app
   reads them from the Windows Credential Manager; the MAUI app uses an interactive sign-in).
9. **Validation before start**: the host can run a pre-flight check and show problems (missing files, invalid
   metadata) before the user confirms.

## Should have

10. Pause / resume while running (without closing the app).
11. Estimated time remaining.
12. Thumbnails for image files in the MAUI app (the WPF app has its own).

## Won't have (v1)

- Downloads / reverse migration.
- A UI component library. Hosts build their own UI.

## Non-functional

- Runs on .NET Framework 4.8, .NET 8+, and .NET MAUI (Windows, Android).
- Must not block the calling thread; must not require a particular UI framework or synchronization context.
- Must be usable from dependency-injection and non-DI hosts.
- Public API changes must be backwards compatible within a major version.
