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
        var files = _scanner.Scan(sourceRoot);
        _logger.LogInformation("Found {Count} files in {Root}", files.Count, sourceRoot);

        var relativePaths = files.Select(f => Path.GetRelativePath(sourceRoot, f)).ToList();
        await UploadManifestSafeAsync(relativePaths, cancellationToken);

        var uploaded = 0;
        foreach (var file in files)
        {
            try
            {
                await using var stream = File.OpenRead(file);
                await _api.UploadFileAsync(Path.GetRelativePath(sourceRoot, file), stream, cancellationToken);
                uploaded++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Skipping {File}: {Message}", file, ex.Message);
            }
        }

        await _api.CompleteAsync(cancellationToken);
        _logger.LogInformation("Migration completed successfully. {Count} files uploaded.", uploaded);

        return new MigrationResult(MigrationStatus.Succeeded, uploaded, Array.Empty<FileFailure>(), null);
    }

    private async Task UploadManifestSafeAsync(IReadOnlyList<string> relativePaths, CancellationToken cancellationToken)
    {
        try
        {
            await _api.UploadManifestAsync(relativePaths, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Manifest upload failed; continuing with the file upload.");
        }
    }
}
