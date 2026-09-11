using System.Collections.Concurrent;
using System.Security.Cryptography;
using Gym.Models.Api;

namespace Gym.MockServer;

public sealed record RequestRecord(
    string Method,
    string Path,
    string RouteKey,
    int StatusCode,
    bool Aborted,
    string? CorrelationId,
    string? IdempotencyKey,
    bool HadAuthorization,
    DateTimeOffset Timestamp);

public sealed class StoredFile
{
    public required string FileId { get; init; }
    public required string Name { get; init; }
    public string? ProjectId { get; init; }
    public long Size { get; init; }
    public required string Sha256 { get; init; }
    public DateTimeOffset StoredAt { get; init; }
    public byte[]? Content { get; init; }

    public StoredFileDto ToDto() => new(FileId, Name, ProjectId, Size, Sha256, StoredAt);
}

public sealed class MigrationRecord
{
    private readonly List<StoredFile> _files = new();

    public required string Id { get; init; }
    public required string CustomerId { get; init; }
    public required string DestinationId { get; init; }
    public required string Name { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public string State { get; set; } = MigrationStates.Created;
    public DateTimeOffset UpdatedAt { get; set; }

    public IReadOnlyList<StoredFile> Files
    {
        get { lock (_files) { return _files.ToArray(); } }
    }

    public void AddFile(StoredFile file, DateTimeOffset now)
    {
        lock (_files)
        {
            _files.Add(file);
            if (State == MigrationStates.Created)
            {
                State = MigrationStates.Uploading;
            }

            UpdatedAt = now;
        }
    }

    public MigrationDto ToDto()
    {
        var files = Files;
        return new MigrationDto(Id, CustomerId, DestinationId, Name, State, files.Count, files.Sum(f => f.Size), CreatedAt);
    }

    public MigrationStatusDto ToStatusDto()
    {
        var files = Files;
        return new MigrationStatusDto(Id, State, files.Count, files.Sum(f => f.Size), UpdatedAt);
    }
}

public sealed class UploadSession
{
    private IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

    public required string UploadId { get; init; }
    public required string MigrationId { get; init; }
    public required string FileName { get; init; }
    public string? ProjectId { get; init; }
    public long Length { get; init; }
    public required string ExpectedSha256 { get; init; }
    public long Received { get; private set; }
    public bool Completed { get; set; }
    public object SyncRoot { get; } = new();

    public void Append(ReadOnlySpan<byte> data)
    {
        _hash.AppendData(data);
        Received += data.Length;
    }

    public void ResetProgress()
    {
        _hash.Dispose();
        _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Received = 0;
    }

    public string FinishHash() => Convert.ToHexStringLower(_hash.GetHashAndReset());

    public UploadSessionDto ToDto() => new(UploadId, MigrationId, FileName, Length, Received, Completed);
}

/// <summary>All server-side state. Everything is in memory; Reset() returns to a clean slate.</summary>
public sealed class MockServerState(TimeProvider time)
{
    private int _idCounter;
    private int _tokenRequests;

    public TimeProvider Time { get; } = time;
    public ConcurrentDictionary<string, DateTimeOffset> Tokens { get; } = new();
    public ConcurrentDictionary<string, ProvisionResponse> Destinations { get; } = new(StringComparer.OrdinalIgnoreCase);
    public ConcurrentDictionary<string, MigrationRecord> Migrations { get; } = new(StringComparer.OrdinalIgnoreCase);
    public ConcurrentDictionary<string, UploadSession> Uploads { get; } = new();
    public ConcurrentDictionary<string, object> IdempotentResults { get; } = new();
    public ConcurrentQueue<RequestRecord> Requests { get; } = new();

    /// <summary>When true, the token endpoint rejects the client credentials (simulates a revoked client).</summary>
    public bool CredentialsRevoked { get; set; }

    public int TokenRequestCount => Volatile.Read(ref _tokenRequests);

    public DateTimeOffset Now => Time.GetUtcNow();

    public string NextId(string prefix) => $"{prefix}-{Interlocked.Increment(ref _idCounter):D5}";

    internal void CountTokenRequest() => Interlocked.Increment(ref _tokenRequests);

    public string IssueToken(TimeSpan lifetime)
    {
        var token = "tok_" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(12));
        Tokens[token] = Now + lifetime;
        return token;
    }

    public bool IsTokenValid(string token) => Tokens.TryGetValue(token, out var expires) && expires > Now;

    public void RevokeAllTokens() => Tokens.Clear();

    public IReadOnlyList<RequestRecord> RequestsTo(string routeKey) =>
        Requests.Where(r => string.Equals(r.RouteKey, routeKey, StringComparison.OrdinalIgnoreCase)).ToArray();

    public IReadOnlyList<StoredFile> AllStoredFiles() =>
        Migrations.Values.SelectMany(m => m.Files).ToArray();

    public void Reset()
    {
        Tokens.Clear();
        Destinations.Clear();
        Migrations.Clear();
        Uploads.Clear();
        IdempotentResults.Clear();
        Requests.Clear();
        CredentialsRevoked = false;
        Interlocked.Exchange(ref _tokenRequests, 0);
    }
}
