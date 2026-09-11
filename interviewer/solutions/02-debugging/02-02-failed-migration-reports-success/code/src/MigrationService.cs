using Microsoft.Extensions.Logging;

namespace MigrationKit.Migration;

public sealed class MigrationService
{
    private readonly IMigrationApi _api;
    private readonly SourceScanner _scanner;
    private readonly ILogger<MigrationService> _logger;

    public MigrationService(IMigrationApi api, SourceScanner scanner, ILogger<MigrationService> logger)
    {
        _api = api;
        _scanner = scanner;
        _logger = logger;
    }

    public async Task<MigrationResult> RunAsync(string sourceRoot, CancellationToken cancellationToken = default)
    {
        // --- Fatal: without a readable source or an accepted manifest there is no migration.
        ScanResult scan;
        try
        {
            scan = _scanner.Scan(sourceRoot);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Source folder {Root} could not be read", sourceRoot);
            return Failed($"The source folder '{sourceRoot}' could not be read: {ex.Message}");
        }

        var relativePaths = scan.Files.Select(f => Path.GetRelativePath(sourceRoot, f)).ToList();
        _logger.LogInformation("Found {Count} files in {Root} ({Inaccessible} folders inaccessible)", relativePaths.Count, sourceRoot, scan.InaccessibleFolders.Count);

        try
        {
            await _api.UploadManifestAsync(relativePaths, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "The platform rejected the migration manifest");
            return Failed($"The platform rejected the migration manifest: {ex.Message}");
        }

        // --- Per file: record and continue. Cancellation is never a "file failure".
        var failures = scan.InaccessibleFolders
            .Select(folder => new FileFailure(Path.GetRelativePath(sourceRoot, folder), "Folder could not be read."))
            .ToList();
        var uploaded = 0;

        foreach (var (file, relativePath) in scan.Files.Zip(relativePaths))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
                await _api.UploadFileAsync(relativePath, stream, cancellationToken);
                uploaded++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or HttpRequestException)
            {
                _logger.LogWarning(ex, "File {File} could not be migrated", relativePath);
                failures.Add(new FileFailure(relativePath, ex.Message));
            }
        }

        if (failures.Count > 0)
        {
            _logger.LogWarning("Migration finished with {Failed} failed item(s); not marking it complete.", failures.Count);
            return new MigrationResult(MigrationStatus.PartiallySucceeded, uploaded, failures, $"{failures.Count} item(s) could not be migrated.");
        }

        await _api.CompleteAsync(cancellationToken);
        _logger.LogInformation("Migration completed. {Count} files uploaded.", uploaded);
        return new MigrationResult(MigrationStatus.Succeeded, uploaded, Array.Empty<FileFailure>(), null);
    }

    private static MigrationResult Failed(string error) => new(MigrationStatus.Failed, 0, Array.Empty<FileFailure>(), error);
}
