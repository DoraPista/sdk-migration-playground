using System.Text.Json;
using Gym.Models.Data;

namespace Gym.TestUtilities;

/// <summary>Locates shared/MockData by walking up from the test's output directory.</summary>
public static class MockDataPaths
{
    private static readonly Lazy<string> RootPath = new(FindRoot);

    public static string Root => RootPath.Value;

    public static string Path(params string[] parts) => System.IO.Path.Combine([Root, .. parts]);

    public static string SampleFile(string relativePath) => Path(["files", .. relativePath.Split('/')]);

    public static T Load<T>(params string[] parts) =>
        JsonSerializer.Deserialize<T>(File.ReadAllText(Path(parts)), MockDataJson.Options)
        ?? throw new InvalidDataException($"{string.Join('/', parts)} is empty.");

    public static IReadOnlyList<FileRecord> Files(string dataset = "") =>
        Load<List<FileRecord>>(DatasetParts(dataset, "files.json"));

    public static IReadOnlyList<ProjectRecord> Projects(string dataset = "") =>
        Load<List<ProjectRecord>>(DatasetParts(dataset, "projects.json"));

    public static IReadOnlyList<CustomerRecord> Customers() => Load<List<CustomerRecord>>("customers.json");

    private static string[] DatasetParts(string dataset, string file) =>
        string.IsNullOrEmpty(dataset) ? [file] : ["datasets", dataset, file];

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = System.IO.Path.Combine(dir.FullName, "shared", "MockData");
            if (File.Exists(System.IO.Path.Combine(candidate, ".mockdata-root")))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("Could not find shared/MockData above " + AppContext.BaseDirectory);
    }
}
