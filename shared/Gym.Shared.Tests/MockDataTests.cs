using Gym.TestUtilities;

namespace Gym.Shared.Tests;

public sealed class MockDataTests
{
    [Fact]
    public void Normal_dataset_matches_files_on_disk_except_the_documented_problems()
    {
        var manifest = MockDataPaths.Files();

        var missing = manifest.Where(f => !File.Exists(MockDataPaths.SampleFile(f.RelativePath))).Select(f => f.Id).ToArray();
        var mismatched = manifest
            .Where(f => File.Exists(MockDataPaths.SampleFile(f.RelativePath)))
            .Where(f => TestFiles.Sha256Hex(MockDataPaths.SampleFile(f.RelativePath)) != f.Sha256)
            .Select(f => f.Id)
            .ToArray();

        Assert.Equal(["F-0015"], missing);
        Assert.Equal(["F-0014"], mismatched);
        Assert.Contains(manifest, f => f.SizeBytes == 0);
    }

    [Fact]
    public void Large_and_malformed_datasets_load()
    {
        Assert.Equal(5_000, MockDataPaths.Files("large").Count);
        Assert.Equal(150, MockDataPaths.Projects("large").Count);
        Assert.True(MockDataPaths.Files("malformed").Count >= 10);
    }
}
