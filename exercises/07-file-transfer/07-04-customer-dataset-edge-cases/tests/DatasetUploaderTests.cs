using System.Net.Http.Headers;
using System.Net.Http.Json;
using Gym.TestUtilities;
using MigrationKit.Dataset;

namespace Ex0704.Dataset.Tests;

/// <summary>Migrates Northwind's real export (shared/MockData) to the local mock platform.</summary>
public sealed class DatasetUploaderTests : IClassFixture<MockServerFixture>
{
    private readonly MockServerFixture _server;
    private readonly HttpClient _http;

    public DatasetUploaderTests(MockServerFixture server)
    {
        _server = server;
        _server.Reset();
        _http = new HttpClient { BaseAddress = server.BaseAddress };
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", server.Server.IssueToken());
    }

    [Fact]
    public async Task Every_intact_file_arrives_with_its_path_and_name()
    {
        var migrationId = await CreateMigrationAsync();
        var manifest = MockDataPaths.Files();
        var intact = manifest.Where(f => f.Id is not "F-0014" and not "F-0015").ToArray();

        await Uploader().UploadDatasetAsync(migrationId, MockDataPaths.Path("files"), MockDataPaths.Path("files.json"));

        var stored = _server.State.Migrations[migrationId].Files;
        Assert.Equal(intact.Select(f => f.RelativePath).Order(StringComparer.Ordinal), stored.Select(f => f.Name).Order(StringComparer.Ordinal));
        Assert.All(intact, entry => Assert.Equal(entry.Sha256, stored.Single(s => s.Name == entry.RelativePath).Sha256));
    }

    [Theory]
    [InlineData("documents/Übersicht Grundriss.txt")]
    [InlineData("images/設計図-01.png")]
    [InlineData("documents/Résumé – José Núñez.txt")]
    [InlineData("documents/naïve café menu 😀.txt")]
    [InlineData("documents/empty-notes.txt")]
    public async Task Awkward_files_survive_the_round_trip(string relativePath)
    {
        var migrationId = await CreateMigrationAsync();

        await Uploader().UploadDatasetAsync(migrationId, MockDataPaths.Path("files"), MockDataPaths.Path("files.json"));

        Assert.Contains(_server.State.Migrations[migrationId].Files, f => f.Name == relativePath);
    }

    [Fact]
    public async Task Missing_and_damaged_files_are_reported_and_not_uploaded()
    {
        var migrationId = await CreateMigrationAsync();

        var report = await Uploader().UploadDatasetAsync(migrationId, MockDataPaths.Path("files"), MockDataPaths.Path("files.json"));

        Assert.Contains(report.Problems, p => p.FileId == "F-0015" && p.Kind == DatasetProblemKind.MissingFile);
        Assert.Contains(report.Problems, p => p.FileId == "F-0014" && p.Kind == DatasetProblemKind.ContentMismatch);
        Assert.Equal(2, report.Problems.Count);
        Assert.Equal(17, report.FilesUploaded);
        Assert.DoesNotContain(_server.State.Migrations[migrationId].Files, f => f.Name == "images/site-photo-017.jpg");
    }

    private DatasetUploader Uploader() => new(_http);

    private async Task<string> CreateMigrationAsync()
    {
        var response = await _http.PostAsJsonAsync("migrations", new { customerId = "CUST-1001", destinationId = "dst-00011", name = "Northwind export" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Created>())!.Id;
    }

    private sealed record Created(string Id);
}
