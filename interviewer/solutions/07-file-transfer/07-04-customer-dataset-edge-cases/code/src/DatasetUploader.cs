using System.Net.Http.Json;
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
        await using var manifestStream = File.OpenRead(manifestPath);
        var manifest = await JsonSerializer.DeserializeAsync<List<ManifestEntry>>(manifestStream, Json, cancellationToken).ConfigureAwait(false)
                       ?? throw new InvalidDataException("The manifest is empty.");

        var uploaded = 0;
        var problems = new List<DatasetProblem>();

        foreach (var entry in manifest)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var problem = await UploadEntryAsync(migrationId, datasetRoot, entry, cancellationToken).ConfigureAwait(false);
                if (problem is null)
                {
                    uploaded++;
                    FileCompleted?.Invoke(this, new FileCompletedEventArgs(entry.Id, 100)); // an empty file is 100% done too
                }
                else
                {
                    problems.Add(problem);
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException)
            {
                problems.Add(new DatasetProblem(entry.Id, DatasetProblemKind.UploadFailed, ex.Message));
            }
        }

        return new DatasetReport(uploaded, problems);
    }

    private async Task<DatasetProblem?> UploadEntryAsync(string migrationId, string datasetRoot, ManifestEntry entry, CancellationToken cancellationToken)
    {
        // Manifest paths use '/', whatever the OS; normalise before touching the file system.
        var path = Path.Combine(datasetRoot, entry.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
        {
            return new DatasetProblem(entry.Id, DatasetProblemKind.MissingFile, $"'{entry.RelativePath}' is listed in the manifest but does not exist.");
        }

        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        var sha256 = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));

        // The customer's manifest is the reference: migrating a silently damaged file would destroy the evidence.
        if (entry.Sha256 is not null && !string.Equals(entry.Sha256, sha256, StringComparison.OrdinalIgnoreCase))
        {
            return new DatasetProblem(entry.Id, DatasetProblemKind.ContentMismatch,
                $"'{entry.RelativePath}' does not match the manifest (expected {entry.Sha256}, found {sha256}).");
        }

        stream.Position = 0;
        using var request = new HttpRequestMessage(HttpMethod.Post, $"migrations/{Uri.EscapeDataString(migrationId)}/files?projectId={Uri.EscapeDataString(entry.ProjectId)}")
        {
            Content = new StreamContent(stream),
        };

        // HTTP header values must be ASCII. The contract says: percent-encoded UTF-8. Send the relative path, since the
        // platform shows the folder structure.
        request.Headers.Add("X-File-Name", Uri.EscapeDataString(entry.RelativePath));
        request.Headers.Add("X-Content-SHA256", sha256);

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return new DatasetProblem(entry.Id, DatasetProblemKind.UploadFailed, $"{(int)response.StatusCode}: {detail}");
        }

        return null;
    }
}
