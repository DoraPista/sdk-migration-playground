namespace MigrationKit.Planning;

public sealed record FileEntry(string Id, string ProjectId, string RelativePath, long SizeBytes);

public sealed record ProjectInfo(string Id, string Name, string? ParentId);

public sealed record PlanItem(string FileId, string ProjectPath, string RelativePath, long SizeBytes);

public sealed record MigrationPlan(IReadOnlyList<PlanItem> Items, long TotalBytes, int DuplicatePaths);

/// <summary>The platform's project catalog. Every call is a request over the network.</summary>
public interface IProjectCatalog
{
    Task<ProjectInfo> GetProjectAsync(string projectId, CancellationToken cancellationToken = default);

    /// <summary>Fetches many projects in one request.</summary>
    Task<IReadOnlyList<ProjectInfo>> GetProjectsAsync(IReadOnlyList<string> projectIds, CancellationToken cancellationToken = default);
}
