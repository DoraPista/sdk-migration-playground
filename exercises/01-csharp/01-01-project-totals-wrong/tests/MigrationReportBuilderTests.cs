using Gym.TestUtilities;
using MigrationKit.Reporting;

namespace Ex0101.Reporting.Tests;

public sealed class MigrationReportBuilderTests
{
    private static readonly DateTimeOffset T0 = new(2026, 3, 2, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Totals_add_up_for_a_simple_migration()
    {
        var report = new MigrationReportBuilder().Build(
        [
            new UploadResult("F-1", "PRJ-1", 100, true, null, T0),
            new UploadResult("F-2", "PRJ-1", 50, true, null, T0.AddMinutes(1)),
            new UploadResult("F-3", "PRJ-2", 0, false, "HTTP 500", T0.AddMinutes(2)),
        ]);

        Assert.Equal(2, report.FilesUploaded);
        Assert.Equal(1, report.FilesFailed);
        Assert.Equal(150, report.BytesUploaded);
        Assert.Equal(["F-3"], report.FailedFileIds);
    }

    [Fact]
    public void Each_project_is_listed_once_using_its_canonical_id()
    {
        var report = new MigrationReportBuilder().Build(
        [
            new UploadResult("F-1", "PRJ-2002", 10, true, null, T0),
            new UploadResult("F-2", "prj-2002", 20, true, null, T0),
            new UploadResult("F-3", "PRJ-2002 ", 30, true, null, T0),
        ]);

        var project = Assert.Single(report.Projects);
        Assert.Equal("PRJ-2002", project.ProjectId);
        Assert.Equal(3, project.FilesUploaded);
        Assert.Equal(60, project.BytesUploaded);
    }

    [Fact]
    public void A_file_that_succeeded_after_a_retry_is_not_reported_as_failed()
    {
        var report = new MigrationReportBuilder().Build(
        [
            new UploadResult("F-9", "PRJ-1", 700, true, null, T0.AddMinutes(30)),
            new UploadResult("F-9", "PRJ-1", 0, false, "HTTP 503 after 3 attempts", T0.AddMinutes(5)),
        ]);

        Assert.Equal(1, report.FilesUploaded);
        Assert.Equal(0, report.FilesFailed);
        Assert.Empty(report.FailedFileIds);
    }

    [Fact]
    public void A_file_reported_twice_is_counted_once()
    {
        var report = new MigrationReportBuilder().Build(
        [
            new UploadResult("F-1", "PRJ-1", 100, true, null, T0),
            new UploadResult("F-1", "PRJ-1", 100, true, null, T0.AddSeconds(1)),
        ]);

        Assert.Equal(1, report.FilesUploaded);
        Assert.Equal(100, report.BytesUploaded);
    }

    [Fact]
    public void Northwind_results_file_produces_a_consistent_report()
    {
        var results = UploadResultsFile.Read(MockDataPaths.Path("datasets", "results", "upload-results.json"));
        var distinctFiles = results.Select(r => r.FileId).Distinct().Count();

        var report = new MigrationReportBuilder().Build(results);

        Assert.Equal(distinctFiles, report.FilesUploaded + report.FilesFailed);
        Assert.Equal(report.Projects.Sum(p => p.BytesUploaded), report.BytesUploaded);
        Assert.DoesNotContain("F-R009", report.FailedFileIds);
        Assert.Equal(
            report.Projects.Count,
            report.Projects.Select(p => p.ProjectId.Trim().ToUpperInvariant()).Distinct().Count());
    }
}
