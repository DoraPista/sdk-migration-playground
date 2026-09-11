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
        // 1. One request instead of one per file: fetch every project the export mentions, plus their ancestors.
        var projects = await LoadProjectsAsync(files, cancellationToken).ConfigureAwait(false);

        // 2. Each project's path is computed once and reused.
        var pathCache = new Dictionary<string, string>(StringComparer.Ordinal);

        var items = new List<PlanItem>(files.Count);
        var seenPaths = new HashSet<string>(StringComparer.Ordinal); // 3. O(1) duplicate detection instead of O(n²)
        var duplicatePaths = 0;
        long totalBytes = 0;

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!seenPaths.Add(file.RelativePath))
            {
                duplicatePaths++;
            }

            items.Add(new PlanItem(file.Id, ProjectPath(file.ProjectId, projects, pathCache), file.RelativePath, file.SizeBytes));
            totalBytes += file.SizeBytes;
        }

        // 4. The wizard only needs the final number: serialize once, not once per file.
        PlanSizeInBytes = JsonSerializer.Serialize(items).Length;

        return new MigrationPlan(items, totalBytes, duplicatePaths);
    }

    /// <summary>Loads the projects of the export and every ancestor, in as few requests as possible.</summary>
    private async Task<Dictionary<string, ProjectInfo>> LoadProjectsAsync(IReadOnlyList<FileEntry> files, CancellationToken cancellationToken)
    {
        var known = new Dictionary<string, ProjectInfo>(StringComparer.Ordinal);
        var wanted = files.Select(f => f.ProjectId).Distinct(StringComparer.Ordinal).ToList();

        while (wanted.Count > 0)
        {
            foreach (var project in await _catalog.GetProjectsAsync(wanted, cancellationToken).ConfigureAwait(false))
            {
                known[project.Id] = project;
            }

            // Ancestors we haven't seen yet (project trees are shallow, so this loops a handful of times).
            wanted = known.Values
                .Select(p => p.ParentId)
                .Where(id => id is { Length: > 0 } && !known.ContainsKey(id))
                .Select(id => id!)
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        return known;
    }

    private static string ProjectPath(string projectId, Dictionary<string, ProjectInfo> projects, Dictionary<string, string> cache)
    {
        if (cache.TryGetValue(projectId, out var cached))
        {
            return cached;
        }

        if (!projects.TryGetValue(projectId, out var project))
        {
            return cache[projectId] = projectId; // unknown project: keep the id rather than failing the plan
        }

        var path = project.ParentId is { Length: > 0 } parentId && parentId != projectId
            ? $"{ProjectPath(parentId, projects, cache)} / {project.Name}"
            : project.Name;

        return cache[projectId] = path;
    }
}
