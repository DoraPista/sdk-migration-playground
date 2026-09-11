using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Gym.MockServer;

// Shows, against the real (local) platform, what an ambiguous upload looks like from both sides.

await using var platform = await MockServer.StartAsync();
using var http = new HttpClient { BaseAddress = platform.BaseAddress };
http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", platform.IssueToken());

var created = await http.PostAsJsonAsync("migrations", new { customerId = "CUST-1001", destinationId = "dst-00011", name = "Harbour Bridge archive" });
var migrationId = (await created.Content.ReadFromJsonAsync<Migration>())!.Id;
Console.WriteLine($"Migration {migrationId} created on {platform.BaseAddress}");

// The platform will process the next upload and then lose the connection before answering.
platform.Faults.Simulate(FailureMode.ResponseLost, "POST /migrations/{id}/files");

var content = Encoding.UTF8.GetBytes(new string('x', 18 * 1024));
var fileName = "documents/tender.pdf";

Console.WriteLine($"\nUploading {fileName} ({content.Length:N0} bytes) ...");
try
{
    using var request = new HttpRequestMessage(HttpMethod.Post, $"migrations/{migrationId}/files")
    {
        Content = new ByteArrayContent(content),
    };
    request.Headers.Add("X-File-Name", Uri.EscapeDataString(fileName));
    request.Headers.Add("X-Content-SHA256", Convert.ToHexStringLower(SHA256.HashData(content)));

    using var response = await http.SendAsync(request);
    Console.WriteLine($"The client saw: {(int)response.StatusCode} {response.ReasonPhrase}");
}
catch (Exception ex)
{
    Console.WriteLine($"The client saw: {ex.GetType().Name}: {ex.Message}");
    if (ex.InnerException is { } inner)
    {
        Console.WriteLine($"                inner: {inner.GetType().Name}: {inner.Message}");
    }
}

var stored = platform.State.Migrations[migrationId].Files;
Console.WriteLine($"\nThe platform stored {stored.Count} file(s):");
foreach (var file in stored)
{
    Console.WriteLine($"  {file.FileId}  {file.Name}  {file.Size:N0} bytes  sha256={file.Sha256[..16]}…");
}

Console.WriteLine("\nWhat the client can ask the platform afterwards:");
var listed = await http.GetFromJsonAsync<StoredFile[]>($"migrations/{migrationId}/files?name={Uri.EscapeDataString(fileName)}");
Console.WriteLine($"  GET /migrations/{migrationId}/files?name={fileName} -> {listed!.Length} match(es)");
foreach (var file in listed)
{
    Console.WriteLine($"     {file.FileId}  size={file.Size:N0}  sha256={file.Sha256[..16]}…");
}

Console.WriteLine("\nNow the same upload again, with an Idempotency-Key, to see what the platform does:");
for (var attempt = 1; attempt <= 2; attempt++)
{
    using var request = new HttpRequestMessage(HttpMethod.Post, $"migrations/{migrationId}/files")
    {
        Content = new ByteArrayContent(content),
    };
    request.Headers.Add("X-File-Name", Uri.EscapeDataString("documents/retendered.pdf"));
    request.Headers.Add("X-Content-SHA256", Convert.ToHexStringLower(SHA256.HashData(content)));
    request.Headers.Add("Idempotency-Key", "demo-key-1");

    using var response = await http.SendAsync(request);
    var file = await response.Content.ReadFromJsonAsync<StoredFile>();
    Console.WriteLine($"  attempt {attempt}: {(int)response.StatusCode} -> fileId {file!.FileId}");
}

Console.WriteLine($"\nThe platform now holds {platform.State.Migrations[migrationId].Files.Count} file(s) in total.");

internal sealed record Migration(string Id);

internal sealed record StoredFile(string FileId, string Name, long Size, string Sha256);
