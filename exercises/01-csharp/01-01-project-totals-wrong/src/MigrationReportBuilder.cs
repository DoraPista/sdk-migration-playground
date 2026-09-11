namespace MigrationKit.Reporting;

public sealed class MigrationReportBuilder
{
    public MigrationReport Build(IEnumerable<UploadResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        var byProject = new Dictionary<string, ProjectSummary>();
        var failedFiles = new List<string>();

        foreach (var result in results)
        {
            if (!byProject.TryGetValue(result.ProjectId, out var summary))
            {
                summary = new ProjectSummary(result.ProjectId, 0, 0, 0);
            }

            if (result.Succeeded)
            {
                summary = summary with
                {
                    FilesUploaded = summary.FilesUploaded + 1,
                    BytesUploaded = summary.BytesUploaded + result.Bytes,
                };
            }
            else
            {
                summary = summary with { FilesFailed = summary.FilesFailed + 1 };
                failedFiles.Add(result.FileId);
            }

            byProject[result.ProjectId] = summary;
        }

        var projects = byProject.Values
            .OrderBy(p => p.ProjectId, StringComparer.Ordinal)
            .ToList();

        return new MigrationReport(
            projects,
            projects.Sum(p => p.FilesUploaded),
            projects.Sum(p => p.FilesFailed),
            projects.Sum(p => p.BytesUploaded))
        {
            FailedFileIds = failedFiles,
        };
    }
}
