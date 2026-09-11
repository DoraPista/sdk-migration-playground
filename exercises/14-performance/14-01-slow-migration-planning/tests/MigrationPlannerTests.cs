using Gym.TestUtilities;
using MigrationKit.Planning;

namespace Ex1401.Planning.Tests;

// One class: the allocation test measures the whole process, so nothing may run beside it.
public sealed class MigrationPlannerTests
{
    private const long OneMegabyte = 1024 * 1024;

    [Fact]
    public async Task Plan_lists_every_file_with_its_full_project_path()
    {
        var catalog = new FakeCatalog(SmallCatalog);

        var plan = await new MigrationPlanner(catalog).BuildPlanAsync(SmallExport).WithTimeout();

        Assert.Equal(["F-1", "F-2", "F-3"], plan.Items.Select(i => i.FileId));
        Assert.Equal("Harbour Bridge Renovation", plan.Items[0].ProjectPath);
        Assert.Equal("Harbour Bridge Renovation / Structural Survey", plan.Items[1].ProjectPath);
        Assert.Equal("Harbour Bridge Renovation / Structural Survey / Phase 2", plan.Items[2].ProjectPath);
        Assert.Equal(600, plan.TotalBytes);
    }

    [Fact]
    public async Task Files_that_share_a_path_are_counted()
    {
        var catalog = new FakeCatalog(SmallCatalog);
        var files = SmallExport.Append(new FileEntry("F-4", "PRJ-1", "documents/a.pdf", 10)).ToList();

        var plan = await new MigrationPlanner(catalog).BuildPlanAsync(files).WithTimeout();

        Assert.Equal(1, plan.DuplicatePaths);
    }

    [Fact]
    public async Task The_catalog_is_not_asked_about_the_same_project_again_and_again()
    {
        var (files, catalog) = LargeExport();

        var plan = await new MigrationPlanner(catalog).BuildPlanAsync(files).WithTimeout(TimeSpan.FromMinutes(5));

        Assert.Equal(files.Count, plan.Items.Count);
        Assert.True(catalog.Requests <= 200, $"The catalog was asked {catalog.Requests} times for {catalog.DistinctProjectsRequested} distinct projects.");
    }

    [Fact]
    public async Task Planning_a_large_export_does_not_allocate_hundreds_of_megabytes()
    {
        var (files, catalog) = LargeExport();
        var planner = new MigrationPlanner(catalog);
        await planner.BuildPlanAsync(files.Take(20).ToList()); // warm up

        var before = GC.GetTotalAllocatedBytes(precise: true);
        var plan = await planner.BuildPlanAsync(files).WithTimeout(TimeSpan.FromMinutes(5));
        var allocated = GC.GetTotalAllocatedBytes(precise: true) - before;

        Assert.Equal(files.Count, plan.Items.Count);
        Assert.True(allocated < 64 * OneMegabyte, $"Planning {files.Count} files allocated {allocated / OneMegabyte} MB.");
    }

    // ---------------------------------------------------------------- data

    private static readonly IReadOnlyList<FileEntry> SmallExport =
    [
        new("F-1", "PRJ-1", "documents/a.pdf", 100),
        new("F-2", "PRJ-2", "documents/b.pdf", 200),
        new("F-3", "PRJ-3", "images/c.jpg", 300),
    ];

    private static readonly IReadOnlyList<ProjectInfo> SmallCatalog =
    [
        new("PRJ-1", "Harbour Bridge Renovation", null),
        new("PRJ-2", "Structural Survey", "PRJ-1"),
        new("PRJ-3", "Phase 2", "PRJ-2"),
    ];

    /// <summary>1,500 files across the 150 projects of the large mock dataset.</summary>
    private static (IReadOnlyList<FileEntry> Files, FakeCatalog Catalog) LargeExport()
    {
        var files = MockDataPaths.Files("large")
            .Take(1_500)
            .Select(f => new FileEntry(f.Id, f.ProjectId, f.RelativePath, f.SizeBytes))
            .ToList();

        var projects = MockDataPaths.Projects("large")
            .Select(p => new ProjectInfo(p.Id, p.Name, p.ParentId))
            .ToList();

        return (files, new FakeCatalog(projects));
    }

    private sealed class FakeCatalog(IReadOnlyList<ProjectInfo> projects) : IProjectCatalog
    {
        private readonly Dictionary<string, ProjectInfo> _byId = projects.ToDictionary(p => p.Id, StringComparer.Ordinal);
        private readonly HashSet<string> _distinct = new(StringComparer.Ordinal);

        public int Requests { get; private set; }

        public int DistinctProjectsRequested => _distinct.Count;

        public async Task<ProjectInfo> GetProjectAsync(string projectId, CancellationToken cancellationToken = default)
        {
            Requests++;
            _distinct.Add(projectId);
            await Task.Yield(); // a network call is never synchronous
            return _byId[projectId];
        }

        public async Task<IReadOnlyList<ProjectInfo>> GetProjectsAsync(IReadOnlyList<string> projectIds, CancellationToken cancellationToken = default)
        {
            Requests++;
            foreach (var id in projectIds)
            {
                _distinct.Add(id);
            }

            await Task.Yield();
            return projectIds.Where(_byId.ContainsKey).Select(id => _byId[id]).ToList();
        }
    }
}
