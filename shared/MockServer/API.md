# Mock Migration Platform API

A local stand-in for the Azure-hosted Migration Platform. It is the **contract** that the SDK
exercises integrate with. Nothing here needs internet access or a cloud subscription.

## Running it

```bash
# Standalone (port 5080 by default)
dotnet run --project shared/MockServer/Gym.MockServer

# With a failure mode active
dotnet run --project shared/MockServer/Gym.MockServer -- --failure-mode FailFirstTwoAttempts --route "POST /migrations/{id}/files"
```

Tests start the same server in-process on a free port:

```csharp
await using var server = await MockServer.StartAsync();
using var http = server.CreateClient();
```

Default client credentials: `client_id=gym-client`, `client_secret=gym-secret-3f9a1c`.

## Conventions

| Topic | Contract |
|---|---|
| Base URL | `http://127.0.0.1:{port}/` |
| JSON | camelCase, UTF-8 (`application/json`) |
| Errors | `application/problem+json`: `{ type, title, status, detail?, errors? }` |
| Auth | `Authorization: Bearer {access_token}` on every endpoint except `/health` and `/auth/token` |
| Expired/invalid token | `401` with `WWW-Authenticate: Bearer error="invalid_token"` |
| Correlation | Send `X-Correlation-ID`; the server echoes it back and records it |
| Idempotency | `POST /migrations` and `POST /migrations/{id}/files` honour an `Idempotency-Key` header. A repeated key returns the original result and does not create a second resource |
| Rate limiting | `429` may include `Retry-After` (seconds) |

## Endpoints

### `GET /health`
`200 { "status": "Healthy" }`

### `POST /auth/token`
Form (`application/x-www-form-urlencoded`) or JSON body:

```json
{ "grant_type": "client_credentials", "client_id": "gym-client", "client_secret": "..." }
```

`200 { "access_token": "tok_...", "token_type": "Bearer", "expires_in": 3600 }`
`401 { "error": "invalid_client" }`, `400 { "error": "unsupported_grant_type" }`

### `POST /provision`
`{ "customerId": "CUST-1001", "region": "westeurope" }`

- `201` `{ destinationId, customerId, region, createdAt }`
- `409` when the customer already has a destination. The problem body includes `destinationId` of the existing destination.
- `400` validation problem.

### `POST /migrations`
`{ "customerId": "...", "destinationId": "...", "name": "..." }` → `201` `MigrationDto`

```json
{ "id": "mig-00001", "customerId": "...", "destinationId": "...", "name": "...",
  "state": "Created", "fileCount": 0, "bytesReceived": 0, "createdAt": "..." }
```

### `GET /migrations/{id}` → `200 MigrationDto` | `404`
### `GET /migrations/{id}/status` → `200 { id, state, filesReceived, bytesReceived, updatedAt }` | `404`
`state` is one of `Created`, `Uploading`, `Completed`.

### `POST /migrations/{id}/files`
Single-request upload. Body = raw file bytes (`application/octet-stream`).

| Header / query | Required | Notes |
|---|---|---|
| `X-File-Name` | yes | Percent-encoded UTF-8 (`Uri.EscapeDataString`). `?name=` is accepted as an alternative |
| `X-Content-SHA256` | yes | Lower- or upper-case hex SHA-256 of the body |
| `Idempotency-Key` | no | See conventions |
| `?projectId=` | no | Project the file belongs to |

- `201 { fileId, name, projectId, size, sha256, storedAt }`
- `422` checksum mismatch, `409` migration already completed, `404` unknown migration.

### `GET /migrations/{id}/files[?name=]` → `200 StoredFileDto[]`
Lists what the server has actually stored.

### `POST /migrations/{id}/complete` → `200 MigrationDto`

### Resumable uploads
For large files over unreliable links.

1. `POST /migrations/{id}/uploads` `{ fileName, length, sha256, projectId? }` → `201 { uploadId, migrationId, fileName, length, received, completed }`
2. `PUT /uploads/{uploadId}` with `Content-Range: bytes {start}-{end}/{total}` and the raw bytes.
   - `200 UploadSessionDto` (with the new `received`)
   - `409 UploadSessionDto` if `start` is not equal to the bytes the server already has. The body says where to continue from.
   - Bytes that arrive before a dropped connection are **kept**.
3. `GET /uploads/{uploadId}` → current `UploadSessionDto` | `404` (unknown or lost session)
4. `POST /uploads/{uploadId}/complete` → `201 StoredFileDto` | `409` (incomplete) | `422` (hash mismatch; session reset)

## Failure simulation

Faults are matched by route key: `"{METHOD} {route template}"`, e.g. `"POST /migrations/{id}/files"`, or `"*"`.
Each fault is consumed once unless added with `Always(...)`.

```csharp
server.Faults.Add("POST /migrations/{id}/files", Fault.Status(503), times: 2);
server.Faults.Add("POST /provision", Fault.TooManyRequests(TimeSpan.FromSeconds(3)));
server.Faults.Simulate(FailureMode.ResponseLost, "POST /migrations/{id}/files");
```

| FailureMode | Behaviour |
|---|---|
| `None` | Clear all faults |
| `FailFirstTwoAttempts` | `503`, `503`, then normal |
| `FailAtPercentage` | Upload endpoints accept ~60% of the body, keep it, then drop the connection |
| `Timeout` | Never respond |
| `ChecksumMismatch` | Upload returns `422` |
| `AuthenticationExpired` | All tokens are revoked; the request gets `401` |
| `RateLimited` | `429` with `Retry-After: 2` |
| `ResponseLost` | The request **is processed**, then the connection is dropped before the response |
| `ConnectionDrop` | The connection is dropped before processing |
| `ServerRestart` | In-flight upload sessions are forgotten; `503` |
| `SlowTransfer` | Upload bodies are read at 256 KB/s |

Individual `Fault` factories: `Status`, `TooManyRequests`, `Delayed`, `Hang`, `DropConnection`,
`MalformedJson`, `ResponseLost`, `DropAtPercentage`, `ChecksumMismatch`, `Slow`, `ExpireTokens`, `Restart`.

### Control API (standalone mode)

| Endpoint | Body |
|---|---|
| `POST /_control/reset` | – |
| `POST /_control/failure-mode` | `{ "mode": "RateLimited", "route": "POST /provision", "times": 3 }` |
| `POST /_control/faults` | `{ "route": "*", "kind": "Status", "statusCode": 502, "times": 1, "retryAfterSeconds": 5 }` |
| `POST /_control/expire-tokens` | – |
| `POST /_control/revoke-credentials` | – |
| `GET /_control/requests` | – (request log) |
| `GET /_control/files` | – (everything stored) |
