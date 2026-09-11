# Proposed changes for MigrationKit 2

1. **Add `PauseAsync()` / `ResumeAsync()` to `IMigrationClient`.**
   Product wants pause and resume while a migration is running.

2. **Add an optional parameter** to the existing method:
   `Task<MigrationHandle> StartAsync(MigrationRequest request, CancellationToken ct = default)`
   becomes
   `Task<MigrationHandle> StartAsync(MigrationRequest request, bool validateFirst = true, CancellationToken ct = default)`.

3. **Change `MigrationClient.StartAsync` to return `ValueTask<MigrationHandle>`** instead of `Task<MigrationHandle>`
   ("it allocates less").

4. **Add a `FailedFiles` property** to the class `MigrationProgress`.

5. **Rename the enum member** `MigrationStage.Uploading` to `MigrationStage.Transferring`
   ("uploading is wrong now that we also copy between cloud tenants").

6. **Insert a new enum member** `MigrationStage.Scanning` between `Preparing = 0` and `Validating = 1`,
   renumbering the ones after it.

7. **Add a new enum member** `MigrationStatus.Expired` at the end of the enum
   (a migration whose upload sessions timed out on the platform).

8. **Seal `MigrationClient`** ("nobody should be deriving from it anyway").

9. **Turn the public field** `public string CorrelationId;` on `MigrationResult` **into a property**
   `public string CorrelationId { get; }`.

10. **Change the parameter type** of `MigrationRequest(string customerId, IEnumerable<MigrationSource> sources)`
    to `IReadOnlyList<MigrationSource>` ("we enumerate it three times anyway").

11. **Change the default** of `MigrationClientOptions.MaxConcurrentUploads` from 4 to 16
    ("our own app sets it anyway, and everyone else gets a faster migration").

12. **Move `MigrationProgress` into a new namespace** `MigrationKit.Progress`
    ("the root namespace is getting crowded").

13. **Mark `MigrationClient.StartSync(string folder)` as `[Obsolete]`** (it is used by one partner).

14. **Add a new overload** `Task<MigrationHandle> StartAsync(MigrationRequest request, MigrationStartOptions options, CancellationToken ct = default)`.
