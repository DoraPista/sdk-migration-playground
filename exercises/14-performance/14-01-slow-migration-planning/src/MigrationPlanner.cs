using System.Text.Json;

namespace MigrationKit.Planning;

public sealed class MigrationPlanner
{
    private readonly IProjectCatalog _catalog;

    public MigrationPlanner(IProjectCatalog catalog)
    {
        _catalog = catalog;
    }

    /// <summary>Size of the serialized plan, shown in the wizard ("this migration will send ~X of metadata").</summary>
    public int PlanSizeInBytes { get; private set; }

    public async Task<MigrationPlan> BuildPlanAsync(IReadOnlyList<FileEntry> files, CancellationToken cancellationToken = default)
    {
        var items = new List<PlanItem>();
        var duplicatePaths = 0;

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var project = await _catalog.GetProjectAsync(file.ProjectId, cancellationToken);
            var projectPath = await BuildProjectPathAsync(project, cancellationToken);

            if (items.Any(i => i.RelativePath == file.RelativePath))
            {
                duplicatePaths++;
            }

            items.Add(new PlanItem(file.Id, projectPath, file.RelativePath, file.SizeBytes));

            // Keep the wizard's "metadata size" up to date as we go.
            PlanSizeInBytes = JsonSerializer.Serialize(items).Length;
        }

        return new MigrationPlan(items, items.Sum(i => i.SizeBytes), duplicatePaths);
    }

    /// <summary>"Harbour Bridge Renovation / Structural Survey / Phase 2".</summary>
    private async Task<string> BuildProjectPathAsync(ProjectInfo project, CancellationToken cancellationToken)
    {
        var names = new List<string> { project.Name };
        var current = project;

        while (current.ParentId is { Length: > 0 } parentId)
        {
            current = await _catalog.GetProjectAsync(parentId, cancellationToken);
            names.Insert(0, current.Name);
        }

        return string.Join(" / ", names);
    }
}
