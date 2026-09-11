using System.Text.Json;

namespace MigrationKit.Reporting;

/// <summary>One line of the agent's results file.</summary>
public sealed record UploadResult(
    string FileId,
    string ProjectId,
    long Bytes,
    bool Succeeded,
    string? Error,
    DateTimeOffset CompletedAt);

public sealed record ProjectSummary(string ProjectId, int FilesUploaded, int FilesFailed, long BytesUploaded);

public sealed record MigrationReport(
    IReadOnlyList<ProjectSummary> Projects,
    int FilesUploaded,
    int FilesFailed,
    long BytesUploaded)
{
    public IReadOnlyList<string> FailedFileIds { get; init; } = [];
}

public static class UploadResultsFile
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<UploadResult> Read(string path) =>
        JsonSerializer.Deserialize<List<UploadResult>>(File.ReadAllText(path), Options) ?? [];
}
