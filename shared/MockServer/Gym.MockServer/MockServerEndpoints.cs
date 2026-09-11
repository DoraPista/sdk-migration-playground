using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Gym.Models.Api;

namespace Gym.MockServer;

internal static partial class MockServerEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));
        app.MapPost("/auth/token", IssueTokenAsync);
        app.MapPost("/provision", ProvisionAsync);
        app.MapPost("/migrations", CreateMigrationAsync);
        app.MapGet("/migrations/{id}", GetMigration);
        app.MapGet("/migrations/{id}/status", GetStatus);
        app.MapGet("/migrations/{id}/files", ListFiles);
        app.MapPost("/migrations/{id}/files", UploadFileAsync);
        app.MapPost("/migrations/{id}/complete", CompleteMigration);
        app.MapPost("/migrations/{id}/uploads", StartUploadAsync);
        app.MapGet("/uploads/{uploadId}", GetUpload);
        app.MapPut("/uploads/{uploadId}", PutChunkAsync);
        app.MapPost("/uploads/{uploadId}/complete", CompleteUpload);
    }

    // ---------------------------------------------------------------- auth

    private static async Task<IResult> IssueTokenAsync(HttpContext context, MockServerState state, MockServerOptions options)
    {
        state.CountTokenRequest();

        string? grantType, clientId, clientSecret;
        if (context.Request.HasFormContentType)
        {
            var form = await context.Request.ReadFormAsync(context.RequestAborted);
            grantType = form["grant_type"];
            clientId = form["client_id"];
            clientSecret = form["client_secret"];
        }
        else
        {
            var body = await TryReadJsonAsync<TokenRequest>(context);
            if (body is null)
            {
                return OAuthError(400, "invalid_request");
            }

            (grantType, clientId, clientSecret) = (body.GrantType, body.ClientId, body.ClientSecret);
        }

        if (grantType != "client_credentials")
        {
            return OAuthError(400, "unsupported_grant_type");
        }

        if (state.CredentialsRevoked || clientId != options.ClientId || clientSecret != options.ClientSecret)
        {
            return OAuthError(401, "invalid_client");
        }

        var token = state.IssueToken(options.TokenLifetime);
        return Results.Ok(new TokenResponse(token, "Bearer", (int)options.TokenLifetime.TotalSeconds));
    }

    // ---------------------------------------------------------------- provisioning

    private static async Task<IResult> ProvisionAsync(HttpContext context, MockServerState state)
    {
        var request = await TryReadJsonAsync<ProvisionRequest>(context);
        if (request is null)
        {
            return Problem(400, "Malformed request body");
        }

        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.CustomerId)) errors["customerId"] = ["The customerId field is required."];
        if (string.IsNullOrWhiteSpace(request.Region)) errors["region"] = ["The region field is required."];
        if (errors.Count > 0)
        {
            return Problem(400, "Validation failed", errors: errors);
        }

        var created = new ProvisionResponse(state.NextId("dst"), request.CustomerId!, request.Region!, state.Now);
        var stored = state.Destinations.GetOrAdd(request.CustomerId!, created);
        if (!ReferenceEquals(stored, created))
        {
            return Results.Json(
                new
                {
                    type = "https://gym.local/problems/already-provisioned",
                    title = "Destination already provisioned",
                    status = 409,
                    destinationId = stored.DestinationId,
                },
                contentType: "application/problem+json",
                statusCode: 409);
        }

        return Results.Created($"/destinations/{created.DestinationId}", created);
    }

    // ---------------------------------------------------------------- migrations

    private static readonly object MigrationCreationGate = new();

    private static async Task<IResult> CreateMigrationAsync(HttpContext context, MockServerState state)
    {
        var request = await TryReadJsonAsync<CreateMigrationRequest>(context);
        if (request is null)
        {
            return Problem(400, "Malformed request body");
        }

        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.CustomerId)) errors["customerId"] = ["The customerId field is required."];
        if (string.IsNullOrWhiteSpace(request.DestinationId)) errors["destinationId"] = ["The destinationId field is required."];
        if (errors.Count > 0)
        {
            return Problem(400, "Validation failed", errors: errors);
        }

        var key = IdempotencyKey(context);
        lock (MigrationCreationGate)
        {
            if (key is not null && state.IdempotentResults.TryGetValue("migration:" + key, out var prior))
            {
                return Results.Json(((MigrationRecord)prior).ToDto(), statusCode: 201);
            }

            var record = new MigrationRecord
            {
                Id = state.NextId("mig"),
                CustomerId = request.CustomerId!,
                DestinationId = request.DestinationId!,
                Name = string.IsNullOrWhiteSpace(request.Name) ? "Migration" : request.Name!,
                CreatedAt = state.Now,
                UpdatedAt = state.Now,
            };
            state.Migrations[record.Id] = record;
            if (key is not null)
            {
                state.IdempotentResults["migration:" + key] = record;
            }

            return Results.Created($"/migrations/{record.Id}", record.ToDto());
        }
    }

    private static IResult GetMigration(string id, MockServerState state) =>
        state.Migrations.TryGetValue(id, out var migration)
            ? Results.Ok(migration.ToDto())
            : Problem(404, "Migration not found", $"No migration with id '{id}'.");

    private static IResult GetStatus(string id, MockServerState state) =>
        state.Migrations.TryGetValue(id, out var migration)
            ? Results.Ok(migration.ToStatusDto())
            : Problem(404, "Migration not found", $"No migration with id '{id}'.");

    private static IResult ListFiles(string id, string? name, MockServerState state)
    {
        if (!state.Migrations.TryGetValue(id, out var migration))
        {
            return Problem(404, "Migration not found", $"No migration with id '{id}'.");
        }

        var files = migration.Files.AsEnumerable();
        if (!string.IsNullOrEmpty(name))
        {
            files = files.Where(f => string.Equals(f.Name, name, StringComparison.Ordinal));
        }

        return Results.Ok(files.Select(f => f.ToDto()).ToArray());
    }

    private static IResult CompleteMigration(string id, MockServerState state)
    {
        if (!state.Migrations.TryGetValue(id, out var migration))
        {
            return Problem(404, "Migration not found", $"No migration with id '{id}'.");
        }

        migration.State = MigrationStates.Completed;
        migration.UpdatedAt = state.Now;
        return Results.Ok(migration.ToDto());
    }

    // ---------------------------------------------------------------- single-request upload

    private static async Task<IResult> UploadFileAsync(string id, HttpContext context, MockServerState state, MockServerOptions options)
    {
        if (!state.Migrations.TryGetValue(id, out var migration))
        {
            return Problem(404, "Migration not found", $"No migration with id '{id}'.");
        }

        if (migration.State == MigrationStates.Completed)
        {
            return Problem(409, "Migration already completed");
        }

        var rawName = context.Request.Headers[ApiHeaders.FileName].FirstOrDefault() ?? context.Request.Query["name"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(rawName))
        {
            return Problem(400, "Validation failed", errors: new() { [ApiHeaders.FileName] = ["A file name is required."] });
        }

        var expectedHash = context.Request.Headers[ApiHeaders.ContentSha256].FirstOrDefault();
        if (expectedHash is null || !Sha256Hex().IsMatch(expectedHash))
        {
            return Problem(400, "Validation failed", errors: new() { [ApiHeaders.ContentSha256] = ["A hex SHA-256 of the content is required."] });
        }

        var key = IdempotencyKey(context);
        var idempotencySlot = key is null ? null : $"file:{id}:{key}";
        if (idempotencySlot is not null && state.IdempotentResults.TryGetValue(idempotencySlot, out var prior))
        {
            return Results.Json(((StoredFile)prior).ToDto(), statusCode: 201);
        }

        var fault = context.Items[MockServerPipeline.FaultItem] as Fault;
        var body = await ReceiveBodyAsync(context, fault, context.Request.ContentLength, options.RetainContentUpToBytes);
        if (body is null)
        {
            return Results.Empty;
        }

        if (fault?.Kind == FaultKind.ChecksumMismatch || !string.Equals(body.Sha256, expectedHash, StringComparison.OrdinalIgnoreCase))
        {
            var actual = fault?.Kind == FaultKind.ChecksumMismatch ? new string('0', 64) : body.Sha256;
            return Problem(422, "Checksum mismatch", $"Expected {expectedHash.ToLowerInvariant()} but the received content hashes to {actual}.");
        }

        var file = new StoredFile
        {
            FileId = state.NextId("file"),
            Name = Uri.UnescapeDataString(rawName),
            ProjectId = context.Request.Query["projectId"].FirstOrDefault(),
            Size = body.Length,
            Sha256 = body.Sha256,
            StoredAt = state.Now,
            Content = body.Content,
        };

        if (idempotencySlot is not null)
        {
            var winner = (StoredFile)state.IdempotentResults.GetOrAdd(idempotencySlot, file);
            if (!ReferenceEquals(winner, file))
            {
                return Results.Json(winner.ToDto(), statusCode: 201);
            }
        }

        migration.AddFile(file, state.Now);
        return Results.Created($"/migrations/{id}/files/{file.FileId}", file.ToDto());
    }

    // ---------------------------------------------------------------- resumable upload sessions

    private static async Task<IResult> StartUploadAsync(string id, HttpContext context, MockServerState state)
    {
        if (!state.Migrations.ContainsKey(id))
        {
            return Problem(404, "Migration not found", $"No migration with id '{id}'.");
        }

        var request = await TryReadJsonAsync<StartUploadRequest>(context);
        if (request is null)
        {
            return Problem(400, "Malformed request body");
        }

        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.FileName)) errors["fileName"] = ["The fileName field is required."];
        if (request.Length < 0) errors["length"] = ["The length must not be negative."];
        if (request.Sha256 is null || !Sha256Hex().IsMatch(request.Sha256)) errors["sha256"] = ["A hex SHA-256 is required."];
        if (errors.Count > 0)
        {
            return Problem(400, "Validation failed", errors: errors);
        }

        var session = new UploadSession
        {
            UploadId = state.NextId("upl"),
            MigrationId = id,
            FileName = request.FileName!,
            ProjectId = request.ProjectId,
            Length = request.Length,
            ExpectedSha256 = request.Sha256!.ToLowerInvariant(),
        };
        state.Uploads[session.UploadId] = session;
        return Results.Created($"/uploads/{session.UploadId}", session.ToDto());
    }

    private static IResult GetUpload(string uploadId, MockServerState state) =>
        state.Uploads.TryGetValue(uploadId, out var session)
            ? Results.Ok(session.ToDto())
            : Problem(404, "Upload session not found", $"No upload session with id '{uploadId}'.");

    private static async Task<IResult> PutChunkAsync(string uploadId, HttpContext context, MockServerState state)
    {
        if (!state.Uploads.TryGetValue(uploadId, out var session))
        {
            return Problem(404, "Upload session not found", $"No upload session with id '{uploadId}'.");
        }

        var range = ContentRange().Match(context.Request.Headers.ContentRange.FirstOrDefault() ?? string.Empty);
        if (!range.Success)
        {
            return Problem(400, "Validation failed", errors: new() { ["Content-Range"] = ["Expected 'bytes {start}-{end}/{total}'."] });
        }

        var start = long.Parse(range.Groups["start"].Value);
        var total = long.Parse(range.Groups["total"].Value);
        if (total != session.Length)
        {
            return Problem(400, "Content-Range total does not match the session length");
        }

        if (start != session.Received)
        {
            // Tell the client where we really are so it can continue from there.
            return Results.Json(session.ToDto(), statusCode: 409);
        }

        var fault = context.Items[MockServerPipeline.FaultItem] as Fault;
        // DropAtPercentage is relative to THIS request's body, so a client that resumes keeps making progress.
        var requestLength = context.Request.ContentLength ?? (session.Length - start);
        var dropAfter = fault?.Kind == FaultKind.DropAtPercentage
            ? Math.Max(1, requestLength * fault.Percentage / 100)
            : long.MaxValue;

        var buffer = new byte[64 * 1024];
        long receivedThisRequest = 0;
        int read;
        while ((read = await context.Request.Body.ReadAsync(buffer, context.RequestAborted)) > 0)
        {
            if (session.Received + read > session.Length)
            {
                return Problem(400, "The body is longer than the declared Content-Range");
            }

            var accepted = (int)Math.Min(read, dropAfter - receivedThisRequest);
            lock (session.SyncRoot)
            {
                session.Append(buffer.AsSpan(0, accepted));
            }

            receivedThisRequest += accepted;
            if (receivedThisRequest >= dropAfter)
            {
                MockServerPipeline.MarkAborted(context);
                return Results.Empty;
            }
        }

        return Results.Ok(session.ToDto());
    }

    private static IResult CompleteUpload(string uploadId, MockServerState state)
    {
        if (!state.Uploads.TryGetValue(uploadId, out var session))
        {
            return Problem(404, "Upload session not found", $"No upload session with id '{uploadId}'.");
        }

        if (state.IdempotentResults.TryGetValue("upload:" + uploadId, out var prior))
        {
            return Results.Json(((StoredFile)prior).ToDto(), statusCode: 201);
        }

        if (session.Received != session.Length)
        {
            return Results.Json(session.ToDto(), statusCode: 409);
        }

        string hash;
        lock (session.SyncRoot)
        {
            hash = session.FinishHash();
        }

        if (!string.Equals(hash, session.ExpectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            session.ResetProgress();
            return Problem(422, "Checksum mismatch", $"Expected {session.ExpectedSha256} but the received content hashes to {hash}. The session was reset.");
        }

        var file = new StoredFile
        {
            FileId = state.NextId("file"),
            Name = session.FileName,
            ProjectId = session.ProjectId,
            Size = session.Length,
            Sha256 = hash,
            StoredAt = state.Now,
        };
        session.Completed = true;
        state.IdempotentResults["upload:" + uploadId] = file;
        state.Migrations[session.MigrationId].AddFile(file, state.Now);
        return Results.Created($"/migrations/{session.MigrationId}/files/{file.FileId}", file.ToDto());
    }

    // ---------------------------------------------------------------- helpers

    private sealed record ReceivedBody(long Length, string Sha256, byte[]? Content);

    /// <summary>Streams the body through a hash. Returns null when a fault aborted the connection.</summary>
    private static async Task<ReceivedBody?> ReceiveBodyAsync(HttpContext context, Fault? fault, long? declaredLength, long retainUpTo)
    {
        var dropAt = fault?.Kind == FaultKind.DropAtPercentage
            ? (declaredLength ?? 1024 * 1024) * fault.Percentage / 100
            : long.MaxValue;

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        MemoryStream? retained = (declaredLength ?? 0) <= retainUpTo ? new MemoryStream() : null;
        var buffer = new byte[64 * 1024];
        long total = 0;
        int read;
        while ((read = await context.Request.Body.ReadAsync(buffer, context.RequestAborted)) > 0)
        {
            hash.AppendData(buffer, 0, read);
            total += read;
            if (retained is not null)
            {
                if (total <= retainUpTo) retained.Write(buffer, 0, read);
                else retained = null;
            }

            if (total >= dropAt)
            {
                MockServerPipeline.MarkAborted(context);
                return null;
            }

            if (fault?.Kind == FaultKind.SlowTransfer)
            {
                await Task.Delay(TimeSpan.FromSeconds((double)read / fault.BytesPerSecond), context.RequestAborted);
            }
        }

        return new ReceivedBody(total, Convert.ToHexStringLower(hash.GetHashAndReset()), retained?.ToArray());
    }

    private static async Task<T?> TryReadJsonAsync<T>(HttpContext context) where T : class
    {
        try
        {
            return await context.Request.ReadFromJsonAsync<T>(context.RequestAborted);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or BadHttpRequestException)
        {
            return null;
        }
    }

    private static string? IdempotencyKey(HttpContext context) =>
        context.Request.Headers[ApiHeaders.IdempotencyKey].FirstOrDefault() is { Length: > 0 } key ? key : null;

    internal static IResult Problem(int status, string title, string? detail = null, Dictionary<string, string[]>? errors = null) =>
        Results.Json(
            new ProblemDto($"https://gym.local/problems/{status}", title, status, detail, errors),
            contentType: "application/problem+json",
            statusCode: status);

    private static IResult OAuthError(int status, string error) =>
        Results.Json(new Dictionary<string, string> { ["error"] = error }, statusCode: status);

    [GeneratedRegex("^[0-9a-fA-F]{64}$")]
    private static partial Regex Sha256Hex();

    [GeneratedRegex(@"^bytes (?<start>\d+)-(?<end>\d+)/(?<total>\d+)$")]
    private static partial Regex ContentRange();
}
