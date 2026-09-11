using System.Security.Cryptography;
using System.Text.Json;

namespace MigrationKit.Dataset;

/// <summary>One entry of the customer's manifest (files.json).</summary>
public sealed record ManifestEntry(string Id, string ProjectId, string RelativePath, long SizeBytes, string? Sha256);

public enum DatasetProblemKind
{
    MissingFile,
    ContentMismatch,
    UploadFailed,
}

public sealed record DatasetProblem(string FileId, DatasetProblemKind Kind, string Detail);

public sealed record DatasetReport(int FilesUploaded, IReadOnlyList<DatasetProblem> Problems);

public sealed record FileCompletedEventArgs(string FileId, int PercentOfFile);

public sealed class DatasetUploader
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;

    public DatasetUploader(HttpClient http)
    {
        _http = http;
    }

    /// <summary>Raised after each file (the desktop app shows a per-file indicator).</summary>
    public event EventHandler<FileCompletedEventArgs>? FileCompleted;

    public async Task<DatasetReport> UploadDatasetAsync(string migrationId, string datasetRoot, string manifestPath, CancellationToken cancellationToken = default)
    {
        var manifest = JsonSerializer.Deserialize<List<ManifestEntry>>(await File.ReadAllTextAsync(manifestPath, cancellationToken), Json)!;
        var uploaded = 0;

        foreach (var entry in manifest)
        {
            var path = Path.Combine(datasetRoot, entry.RelativePath);
            await using var stream = File.OpenRead(path);

            var sha256 = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken));
            stream.Position = 0;

            using var request = new HttpRequestMessage(HttpMethod.Post, $"migrations/{migrationId}/files?projectId={entry.ProjectId}")
            {
                Content = new StreamContent(stream),
            };
            request.Headers.Add("X-File-Name", Path.GetFileName(entry.RelativePath));
            request.Headers.Add("X-Content-SHA256", sha256);

            using var response = await _http.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            uploaded++;
            FileCompleted?.Invoke(this, new FileCompletedEventArgs(entry.Id, (int)(stream.Length * 100 / entry.SizeBytes)));
        }

        return new DatasetReport(uploaded, Array.Empty<DatasetProblem>());
    }
}
