using System.Net;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using Gym.TestUtilities;
using MigrationKit.Transfer;

namespace Ex0904.Transfer.Tests;

public sealed class CompatibilityTests : IDisposable
{
    private readonly TestFiles _files = new();

    [Fact]
    public void Library_can_be_referenced_by_the_customers_net_framework_application()
    {
        var framework = typeof(TransferClient).Assembly.GetCustomAttribute<TargetFrameworkAttribute>()!.FrameworkName;

        Assert.Equal(".NETStandard,Version=v2.0", framework);
    }

    // ---------------------------------------------------------------- behaviour that must not change

    [Fact]
    public async Task Manifest_is_read()
    {
        var path = _files.CreateText("files.json", """
            [
              { "relativePath": "documents/specification.pdf", "size": 48000, "sha256": "aa" },
              { "relativePath": "images/deck-inspection-001.jpg", "size": 182000, "sha256": "bb" }
            ]
            """);

        var entries = await ManifestReader.ReadAsync(path);

        Assert.Equal(2, entries.Count);
        Assert.Equal("documents/specification.pdf", entries[0].RelativePath);
        Assert.Equal(48_000, entries[0].Size);
        Assert.False(entries[0].IsImage);
        Assert.True(entries[1].IsImage);
    }

    [Theory]
    [InlineData(@"C:\Projects\Northwind", @"C:\Projects\Northwind\documents\specification.pdf", "documents/specification.pdf")]
    [InlineData(@"C:\Projects\Northwind\", @"C:\Projects\Northwind\a.txt", "a.txt")]
    [InlineData(@"C:\Projects\Northwind", @"C:\Projects\Northwind\survey\2024\points.csv", "survey/2024/points.csv")]
    public void Manifest_paths_are_relative_and_forward_slashed(string root, string full, string expected)
    {
        Assert.Equal(expected, ManifestReader.ToManifestPath(root, full));
    }

    [Fact]
    public async Task File_hash_is_lower_case_hex()
    {
        var path = _files.CreateText("a.txt", "migration");

        var hash = await Hashing.Sha256Async(path);

        Assert.Equal(TestFiles.Sha256Hex(Encoding.UTF8.GetBytes("migration")), hash);
        Assert.Equal(64, hash.Length);
        Assert.DoesNotContain(hash, char.IsUpper);
    }

    [Theory]
    [InlineData(10, 3, new[] { 3, 3, 3, 1 })]
    [InlineData(4, 4, new[] { 4 })]
    [InlineData(0, 5, new int[0])]
    public void Batches_are_planned_in_order(int entryCount, int batchSize, int[] expectedSizes)
    {
        var entries = Enumerable.Range(1, entryCount).Select(i => new FileEntry($"documents/{i}.pdf", i * 100, "aa")).ToList();

        var batches = UploadPlanner.PlanBatches(entries, batchSize);

        Assert.Equal(expectedSizes, batches.Select(b => b.Count));
        Assert.Equal(entries.Select(e => e.RelativePath), batches.SelectMany(b => b).Select(e => e.RelativePath));
    }

    [Fact]
    public void Total_bytes_are_summed()
    {
        var entries = new[] { new FileEntry("a", 3_000_000_000, "aa"), new FileEntry("b", 2_000_000_000, "bb") };

        Assert.Equal(5_000_000_000, UploadPlanner.TotalBytes(entries));
    }

    [Fact]
    public async Task Batch_registration_posts_the_expected_json()
    {
        var handler = new ScriptedHttpHandler().RespondJson(HttpStatusCode.Created, new { batchId = "batch-1" });
        var client = new TransferClient(new HttpClient(handler) { BaseAddress = new Uri("https://platform.test/") });

        var batchId = await client.RegisterBatchAsync("mig-00102", [new FileEntry("images/a.jpg", 10, "aa")]);

        Assert.Equal("batch-1", batchId);
        var body = JsonDocument.Parse(Assert.Single(handler.Requests).BodyText!).RootElement;
        Assert.Equal("mig-00102", body.GetProperty("migrationId").GetString());
        var item = body.GetProperty("items")[0];
        Assert.Equal("images/a.jpg", item.GetProperty("path").GetString());
        Assert.True(item.GetProperty("image").GetBoolean());
    }

    [Fact]
    public async Task Sending_a_file_streams_all_of_its_bytes()
    {
        var path = _files.Create("scan.e57", 250_000);
        var handler = new ScriptedHttpHandler().OtherwiseRespond(HttpStatusCode.Created);
        var client = new TransferClient(new HttpClient(handler) { BaseAddress = new Uri("https://platform.test/") });

        var sent = await client.SendAsync("https://platform.test/uploads/u1", path);

        Assert.Equal(250_000, sent);
        Assert.Equal(TestFiles.Sha256Hex(path), Assert.Single(handler.Requests).BodySha256);
    }

    [Theory]
    [InlineData("documents/a.pdf", true)]
    [InlineData(@"C:\Projects\a.pdf", false)]
    [InlineData("a.pdf", false)]
    public void Manifest_paths_are_recognised(string path, bool expected)
    {
        Assert.Equal(expected, TransferClient.LooksLikeManifestPath(path));
    }

    public void Dispose() => _files.Dispose();
}
