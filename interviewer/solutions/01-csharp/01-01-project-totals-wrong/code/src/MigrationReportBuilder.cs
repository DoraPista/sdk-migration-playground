namespace MigrationKit.Reporting;

public sealed class MigrationReportBuilder
{
    public MigrationReport Build(IEnumerable<UploadResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        // 1. One outcome per file: the most recent result wins (retries write several lines).
        var latestPerFile = results
            .GroupBy(r => r.FileId, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.MaxBy(r => r.CompletedAt)!)
            .ToList();

        // 2. Group by the canonical project id. The legacy agent lower-cases and pads ids.
        var projects = latestPerFile
            .GroupBy(r => CanonicalProjectId(r.ProjectId))
            .Select(g => new ProjectSummary(
                g.Key,
                g.Count(r => r.Succeeded),
                g.Count(r => !r.Succeeded),
                g.Where(r => r.Succeeded).Sum(r => r.Bytes)))
            .OrderBy(p => p.ProjectId, StringComparer.Ordinal)
            .ToList();

        return new MigrationReport(
            projects,
            projects.Sum(p => p.FilesUploaded),
            projects.Sum(p => p.FilesFailed),
            projects.Sum(p => p.BytesUploaded))
        {
            FailedFileIds = latestPerFile
                .Where(r => !r.Succeeded)
                .Select(r => r.FileId)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList(),
        };
    }

    internal static string CanonicalProjectId(string projectId) => projectId.Trim().ToUpperInvariant();
}
