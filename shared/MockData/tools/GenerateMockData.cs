#:property PublishAot=false
// Regenerates every file in shared/MockData deterministically.
// Usage (from the repository root):
//   dotnet run shared/MockData/tools/GenerateMockData.cs -- shared/MockData
//   dotnet run shared/MockData/tools/GenerateMockData.cs -- shared/MockData --large-files 2048
// The optional --large-files N writes N MB of real content to shared/MockData/generated/ (git-ignored).

using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

var root = args.Length > 0 && !args[0].StartsWith("--") ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
var largeMb = args.SkipWhile(a => a != "--large-files").Skip(1).Select(int.Parse).FirstOrDefault();
var json = new JsonSerializerOptions
{
    WriteIndented = true,
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
};
var epoch = new DateTimeOffset(2026, 3, 2, 8, 0, 0, TimeSpan.Zero);

void Write(string relative, JsonNode node) =>
    File.WriteAllText(Path.Combine(root, relative), node.ToJsonString(json) + "\n", new UTF8Encoding(false));

string Sha(byte[] data) => Convert.ToHexStringLower(SHA256.HashData(data));

byte[] Bytes(int size, int seed, string? textPrefix = null)
{
    var data = new byte[size];
    new Random(seed).NextBytes(data);
    if (textPrefix is not null)
    {
        var text = Encoding.UTF8.GetBytes(textPrefix);
        Array.Copy(text, data, Math.Min(text.Length, size));
    }

    return data;
}

byte[] Text(string content) => Encoding.UTF8.GetBytes(content);

byte[] Csv(int rows, int seed)
{
    var random = new Random(seed);
    var sb = new StringBuilder("pointId,easting,northing,elevation,surveyedAt\n");
    for (var i = 1; i <= rows; i++)
    {
        sb.Append($"P{i:D5},{random.Next(400000, 500000)}.{random.Next(100):D2},{random.Next(5700000, 5800000)}.{random.Next(100):D2},{random.Next(0, 90)}.{random.Next(1000):D3},{epoch.AddMinutes(i):O}\n");
    }

    return Text(sb.ToString());
}

Directory.CreateDirectory(root);
File.WriteAllText(Path.Combine(root, ".mockdata-root"), "Marker file used by test utilities to locate shared/MockData.\n");

// ------------------------------------------------------------------ customers
Write("customers.json", new JsonArray(
    new JsonObject { ["id"] = "CUST-1001", ["name"] = "Northwind Architects", ["region"] = "westeurope", ["tier"] = "Enterprise" },
    new JsonObject { ["id"] = "CUST-1002", ["name"] = "Contoso Civil Engineering", ["region"] = "northeurope", ["tier"] = "Standard" },
    new JsonObject { ["id"] = "CUST-1003", ["name"] = "Fabrikam Surveying GmbH", ["region"] = "germanywestcentral", ["tier"] = "Standard" },
    new JsonObject { ["id"] = "CUST-1004", ["name"] = "Tailspin Heritage Trust", ["region"] = "uksouth", ["tier"] = "Trial" }));

// ------------------------------------------------------------------ projects (normal, nested)
var projects = new (string Id, string Customer, string Name, string? Parent)[]
{
    ("PRJ-2001", "CUST-1001", "Harbour Bridge Renovation", null),
    ("PRJ-2002", "CUST-1001", "Harbour Bridge – Structural Survey", "PRJ-2001"),
    ("PRJ-2003", "CUST-1001", "Harbour Bridge – Lighting", "PRJ-2001"),
    ("PRJ-2004", "CUST-1001", "Lighting – Phase 2", "PRJ-2003"),
    ("PRJ-2005", "CUST-1001", "Riverside Library Extension", null),
    ("PRJ-2006", "CUST-1002", "Ring Road Interchange", null),
    ("PRJ-2007", "CUST-1002", "Ring Road – Drainage", "PRJ-2006"),
    ("PRJ-2008", "CUST-1003", "Altstadt Facade Scan", null),
};
Write("projects.json", new JsonArray(projects.Select((p, i) => (JsonNode)new JsonObject
{
    ["id"] = p.Id, ["customerId"] = p.Customer, ["name"] = p.Name, ["parentId"] = p.Parent,
    ["createdAt"] = epoch.AddDays(-400 + i * 17).ToString("O"),
}).ToArray()));

// ------------------------------------------------------------------ sample files (normal dataset)
var filesDir = Path.Combine(root, "files");
if (Directory.Exists(filesDir))
{
    Directory.Delete(filesDir, recursive: true);
}

var samples = new List<(string Id, string Project, string Path, string Kind, string ContentType, byte[] Data, JsonObject Meta, string? ShaOverride, bool Missing)>
{
    ("F-0001", "PRJ-2001", "documents/project-brief.txt", "Document", "text/plain", Text("Harbour Bridge Renovation – project brief\n\nScope: deck replacement, repainting, lighting upgrade.\n"), new() { ["author"] = "E. Okafor", ["tags"] = new JsonArray("brief", "scope") }, null, false),
    ("F-0002", "PRJ-2001", "documents/specification.pdf", "Document", "application/pdf", Bytes(48_000, 2, "%PDF-1.7\n"), new() { ["author"] = "E. Okafor", ["pages"] = 18 }, null, false),
    ("F-0003", "PRJ-2002", "survey/survey-data.csv", "Document", "text/csv", Csv(3_500, 3), new() { ["instrument"] = "Leica TS16", ["crs"] = "EPSG:25832" }, null, false),
    ("F-0004", "PRJ-2002", "survey/pointcloud-extract.bin", "Asset", "application/octet-stream", Bytes(640_000, 4), new() { ["points"] = 40_000 }, null, false),
    ("F-0005", "PRJ-2002", "images/deck-inspection-001.jpg", "Image", "image/jpeg", Bytes(182_000, 5, "ÿØÿ"), new() { ["capturedAt"] = epoch.AddDays(-30).ToString("O"), ["width"] = 4032, ["height"] = 3024 }, null, false),
    ("F-0006", "PRJ-2002", "images/deck-inspection-002.jpg", "Image", "image/jpeg", Bytes(176_500, 6, "ÿØÿ"), new() { ["capturedAt"] = epoch.AddDays(-30).AddMinutes(4).ToString("O"), ["width"] = 4032, ["height"] = 3024 }, null, false),
    ("F-0007", "PRJ-2003", "drawings/lighting-layout.dwg", "Drawing", "application/acad", Bytes(310_000, 7), new() { ["revision"] = "C" }, null, false),
    ("F-0008", "PRJ-2004", "drawings/lighting-phase2.dwg", "Drawing", "application/acad", Bytes(295_000, 8), new() { ["revision"] = "A" }, null, false),
    ("F-0009", "PRJ-2004", "documents/empty-notes.txt", "Document", "text/plain", Array.Empty<byte>(), new() { ["author"] = "J. Park" }, null, false),
    ("F-0010", "PRJ-2005", "documents/Übersicht Grundriss.txt", "Document", "text/plain", Text("Übersicht über die Grundrisse – Erdgeschoss, 1. OG, 2. OG.\n"), new() { ["language"] = "de" }, null, false),
    ("F-0011", "PRJ-2005", "images/設計図-01.png", "Image", "image/png", Bytes(96_000, 11, "PNG\r\n"), new() { ["width"] = 1600, ["height"] = 1200 }, null, false),
    ("F-0012", "PRJ-2005", "documents/Résumé – José Núñez.txt", "Document", "text/plain", Text("Consultant CV – structural engineering.\n"), new() { ["language"] = "es" }, null, false),
    ("F-0013", "PRJ-2005", "documents/naïve café menu 😀.txt", "Document", "text/plain", Text("Staff café menu for site visitors.\n"), new(), null, false),
    ("F-0014", "PRJ-2006", "images/site-photo-017.jpg", "Image", "image/jpeg", Bytes(210_000, 14, "ÿØÿ"), new() { ["capturedAt"] = epoch.AddDays(-12).ToString("O") }, "9f2c1d0e3b4a5968778695a4b3c2d1e0f9e8d7c6b5a4938271605f4e3d2c1b0a", false),
    ("F-0015", "PRJ-2006", "documents/missing-appendix.pdf", "Document", "application/pdf", Bytes(20_000, 15), new(), null, true),
    ("F-0016", "PRJ-2007", "documents/drainage-report.txt", "Document", "text/plain", Text(string.Concat(Enumerable.Repeat("Drainage capacity assessment – culvert C-12 is undersized for a 1:100 year event.\n", 400))), new() { ["author"] = "M. Lindqvist" }, null, false),
    ("F-0017", "PRJ-2007", "images/facade-render.png", "Image", "image/png", Bytes(1_450_000, 17, "PNG\r\n"), new() { ["width"] = 7680, ["height"] = 4320 }, null, false),
    ("F-0018", "PRJ-2008", "scans/facade-north.e57", "Asset", "application/octet-stream", Bytes(820_000, 18), new() { ["scanner"] = "FARO Focus" }, null, false),
    ("F-0019", "PRJ-2008", "metadata/scan-register.json", "Metadata", "application/json", Text("{\n  \"scans\": [\"facade-north\", \"facade-south\"],\n  \"operator\": \"K. Brandt\"\n}\n"), new(), null, false),
};

var fileRecords = new JsonArray();
foreach (var s in samples)
{
    if (!s.Missing)
    {
        var target = Path.Combine(filesDir, s.Path.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllBytes(target, s.Data);
    }

    fileRecords.Add(new JsonObject
    {
        ["id"] = s.Id, ["projectId"] = s.Project, ["relativePath"] = s.Path, ["kind"] = s.Kind,
        ["sizeBytes"] = s.Data.Length, ["sha256"] = s.ShaOverride ?? Sha(s.Data), ["contentType"] = s.ContentType,
        ["metadata"] = s.Meta,
    });
}

Write("files.json", fileRecords);

// ------------------------------------------------------------------ migrations
Write("migrations.json", new JsonArray(
    new JsonObject { ["id"] = "mig-00101", ["customerId"] = "CUST-1001", ["destinationId"] = "dst-00011", ["name"] = "Harbour Bridge archive", ["state"] = "Completed", ["createdAt"] = epoch.AddDays(-20).ToString("O"), ["completedAt"] = epoch.AddDays(-20).AddHours(3).ToString("O") },
    new JsonObject { ["id"] = "mig-00102", ["customerId"] = "CUST-1001", ["destinationId"] = "dst-00011", ["name"] = "Riverside Library", ["state"] = "Uploading", ["createdAt"] = epoch.AddDays(-1).ToString("O") },
    new JsonObject { ["id"] = "mig-00103", ["customerId"] = "CUST-1002", ["destinationId"] = "dst-00012", ["name"] = "Ring Road", ["state"] = "Failed", ["createdAt"] = epoch.AddDays(-3).ToString("O") },
    new JsonObject { ["id"] = "mig-00104", ["customerId"] = "CUST-1003", ["destinationId"] = "dst-00013", ["name"] = "Altstadt scans", ["state"] = "Created", ["createdAt"] = epoch.ToString("O") },
    new JsonObject { ["id"] = "mig-00105", ["customerId"] = "CUST-1004", ["destinationId"] = "dst-00014", ["name"] = "Trust archive (trial)", ["state"] = "Cancelled", ["createdAt"] = epoch.AddDays(-7).ToString("O") }));

// ------------------------------------------------------------------ checkpoint: app killed at ~63%
var present = samples.Where(s => !s.Missing).ToList();
var totalBytes = present.Sum(s => (long)s.Data.Length);
var checkpointFiles = new JsonArray();
long sent = 0;
var inProgressAssigned = false;
foreach (var s in present)
{
    string status;
    long bytesSent;
    string? remoteId = null, uploadId = null;
    if (sent + s.Data.Length <= totalBytes * 0.60)
    {
        status = "Uploaded";
        bytesSent = s.Data.Length;
        remoteId = $"file-{checkpointFiles.Count + 1:D5}";
    }
    else if (!inProgressAssigned && s.Data.Length > 100_000)
    {
        status = "InProgress";
        bytesSent = s.Data.Length * 3 / 5;
        uploadId = "upl-00042";
        inProgressAssigned = true;
    }
    else
    {
        status = "Pending";
        bytesSent = 0;
    }

    sent += bytesSent;
    checkpointFiles.Add(new JsonObject
    {
        ["fileId"] = s.Id, ["relativePath"] = s.Path, ["status"] = status, ["bytesSent"] = bytesSent,
        ["remoteFileId"] = remoteId, ["uploadId"] = uploadId,
    });
}

var checkpoint = new JsonObject
{
    ["migrationId"] = "mig-00102", ["customerId"] = "CUST-1001", ["stage"] = "UploadFiles",
    ["lastUpdated"] = epoch.AddDays(-1).AddMinutes(47).ToString("O"), ["totalFiles"] = present.Count,
    ["percentComplete"] = Math.Round(100.0 * sent / totalBytes), ["files"] = checkpointFiles,
};
Write("migration-state.json", checkpoint);

// ------------------------------------------------------------------ datasets/large
Directory.CreateDirectory(Path.Combine(root, "datasets", "large"));
var rnd = new Random(2026);
var largeProjects = new JsonArray();
for (var i = 1; i <= 150; i++)
{
    var parent = i > 10 ? $"PRJ-L{rnd.Next(1, i):D4}" : null;
    largeProjects.Add(new JsonObject { ["id"] = $"PRJ-L{i:D4}", ["customerId"] = "CUST-1001", ["name"] = $"Programme {i:D3}", ["parentId"] = parent, ["createdAt"] = epoch.AddDays(-i).ToString("O") });
}

Write(Path.Combine("datasets", "large", "projects.json"), largeProjects);
var largeFiles = new JsonArray();
string[] kinds = ["Document", "Image", "Drawing", "Asset", "Metadata"];
for (var i = 1; i <= 5_000; i++)
{
    var kind = kinds[rnd.Next(kinds.Length)];
    long size = kind switch
    {
        "Image" => rnd.Next(80_000, 25_000_000),
        "Asset" => i % 997 == 0 ? 107_374_182_400L : rnd.Next(1_000_000, 900_000_000),
        _ => rnd.Next(0, 5_000_000),
    };
    var hash = new byte[32];
    rnd.NextBytes(hash);
    largeFiles.Add(new JsonObject
    {
        ["id"] = $"F-L{i:D5}", ["projectId"] = $"PRJ-L{rnd.Next(1, 151):D4}", ["relativePath"] = $"{kind.ToLowerInvariant()}s/item-{i:D5}.dat",
        ["kind"] = kind, ["sizeBytes"] = size, ["sha256"] = Convert.ToHexStringLower(hash), ["contentType"] = "application/octet-stream",
        ["metadata"] = new JsonObject { ["batch"] = i / 250 },
    });
}

Write(Path.Combine("datasets", "large", "files.json"), largeFiles);

// ------------------------------------------------------------------ datasets/malformed
Directory.CreateDirectory(Path.Combine(root, "datasets", "malformed"));
Write(Path.Combine("datasets", "malformed", "projects.json"), new JsonArray(
    new JsonObject { ["id"] = "PRJ-9001", ["customerId"] = "CUST-1002", ["name"] = "Depot Upgrade", ["parentId"] = "PRJ-9003" },
    new JsonObject { ["id"] = "PRJ-9002", ["customerId"] = "CUST-1002", ["name"] = "Depot – Roof", ["parentId"] = "PRJ-9001" },
    new JsonObject { ["id"] = "PRJ-9003", ["customerId"] = "CUST-1002", ["name"] = "Depot – Roof – Gutters", ["parentId"] = "PRJ-9002" },
    new JsonObject { ["id"] = "PRJ-9004", ["customerId"] = "CUST-1002", ["name"] = "Orphaned Works", ["parentId"] = "PRJ-0000" },
    new JsonObject { ["id"] = "PRJ-9005", ["customerId"] = "CUST-1002", ["name"] = "Depot Upgrade (copy)", ["parentId"] = null },
    new JsonObject { ["id"] = "PRJ-9005", ["customerId"] = "CUST-1002", ["name"] = "Depot Upgrade (copy 2)", ["parentId"] = null },
    new JsonObject { ["id"] = "prj-9006", ["customerId"] = "CUST-1002", ["name"] = "Lower-case legacy id", ["parentId"] = null }));

Write(Path.Combine("datasets", "malformed", "files.json"), new JsonArray(
    new JsonObject { ["id"] = "F-9001", ["projectId"] = "PRJ-9005", ["relativePath"] = "documents/tender.pdf", ["kind"] = "Document", ["sizeBytes"] = 125_000, ["sha256"] = Sha(Bytes(10, 1)), ["contentType"] = "application/pdf", ["metadata"] = new JsonObject { ["author"] = "R. Diaz" } },
    new JsonObject { ["id"] = "F-9001", ["projectId"] = "PRJ-9005", ["relativePath"] = "documents/tender-v2.pdf", ["kind"] = "Document", ["sizeBytes"] = 126_400, ["sha256"] = Sha(Bytes(10, 2)), ["contentType"] = "application/pdf", ["metadata"] = new JsonObject() },
    new JsonObject { ["id"] = "F-9002", ["projectId"] = "PRJ-9999", ["relativePath"] = "images/unknown-project.jpg", ["kind"] = "Image", ["sizeBytes"] = 50_000, ["sha256"] = Sha(Bytes(10, 3)), ["contentType"] = "image/jpeg", ["metadata"] = new JsonObject() },
    new JsonObject { ["id"] = "F-9003", ["projectId"] = "PRJ-9005", ["relativePath"] = "images/negative.jpg", ["kind"] = "Image", ["sizeBytes"] = -1, ["sha256"] = Sha(Bytes(10, 4)), ["contentType"] = "image/jpeg", ["metadata"] = new JsonObject() },
    new JsonObject { ["id"] = "F-9004", ["projectId"] = "PRJ-9005", ["relativePath"] = "documents/bad-hash.docx", ["kind"] = "Document", ["sizeBytes"] = 30_000, ["sha256"] = "not-a-hash", ["contentType"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document", ["metadata"] = new JsonObject() },
    new JsonObject { ["id"] = "F-9005", ["projectId"] = "PRJ-9005", ["relativePath"] = "../../Windows/System32/drivers/etc/hosts", ["kind"] = "Document", ["sizeBytes"] = 824, ["sha256"] = Sha(Bytes(10, 5)), ["contentType"] = "text/plain", ["metadata"] = new JsonObject() },
    new JsonObject { ["id"] = "F-9006", ["projectId"] = "PRJ-9005", ["relativePath"] = "", ["kind"] = "Document", ["sizeBytes"] = 10, ["sha256"] = Sha(Bytes(10, 6)), ["contentType"] = "text/plain", ["metadata"] = new JsonObject() },
    new JsonObject { ["id"] = "F-9007", ["projectId"] = "PRJ-9005", ["relativePath"] = "images/photo-with-bad-metadata.jpg", ["kind"] = "Image", ["sizeBytes"] = 95_000, ["sha256"] = Sha(Bytes(10, 7)), ["contentType"] = "image/jpeg", ["metadata"] = new JsonObject { ["capturedAt"] = "last tuesday", ["width"] = "wide", ["tags"] = "not-an-array" } },
    new JsonObject { ["id"] = "F-9008", ["projectId"] = "PRJ-9002", ["relativePath"] = "drawings/roof.dwg", ["kind"] = "Hologram", ["sizeBytes"] = 44_000, ["sha256"] = Sha(Bytes(10, 8)), ["contentType"] = null, ["metadata"] = null },
    new JsonObject { ["id"] = "F-9009", ["projectId"] = "prj-9006", ["relativePath"] = "documents/legacy.txt", ["kind"] = "Document", ["sizeBytes"] = 0, ["sha256"] = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", ["contentType"] = "text/plain", ["metadata"] = new JsonObject() },
    new JsonObject { ["id"] = "F-9010", ["relativePath"] = "documents/no-project.txt", ["kind"] = "Document", ["sizeBytes"] = 12, ["sha256"] = Sha(Bytes(10, 10)), ["contentType"] = "text/plain" }));

File.WriteAllText(Path.Combine(root, "datasets", "malformed", "broken.json"),
    "[\n  { \"id\": \"F-9101\", \"projectId\": \"PRJ-9005\", \"relativePath\": \"documents/a.txt\", \"sizeBytes\": 10 },\n  { \"id\": \"F-9102\", \"projectId\": \"PRJ-9005\", \"relativePath\": \"documents/b.t", new UTF8Encoding(false));

var fullCheckpoint = checkpoint.ToJsonString(json);
File.WriteAllText(Path.Combine(root, "datasets", "malformed", "truncated-state.json"), fullCheckpoint[..(fullCheckpoint.Length * 55 / 100)], new UTF8Encoding(false));

// ------------------------------------------------------------------ datasets/results (for result-processing exercises)
Directory.CreateDirectory(Path.Combine(root, "datasets", "results"));
var results = new JsonArray();
var resultRandom = new Random(7);
string[] projectVariants = ["PRJ-2001", "PRJ-2002", "PRJ-2005", "prj-2002", "PRJ-2005 ", "PRJ-2006"];
for (var i = 1; i <= 40; i++)
{
    var project = projectVariants[resultRandom.Next(projectVariants.Length)];
    var failed = i % 9 == 0;
    results.Add(new JsonObject
    {
        ["fileId"] = $"F-R{i:D3}", ["projectId"] = project, ["bytes"] = failed ? 0 : resultRandom.Next(1_000, 2_000_000),
        ["succeeded"] = !failed, ["error"] = failed ? "HTTP 503 after 3 attempts" : null,
        ["completedAt"] = epoch.AddMinutes(i).ToString("O"),
    });
}

// The same file reported twice (a retry that also reported its first, failed, attempt).
results.Add(new JsonObject { ["fileId"] = "F-R009", ["projectId"] = "PRJ-2005", ["bytes"] = 734_112, ["succeeded"] = true, ["error"] = null, ["completedAt"] = epoch.AddMinutes(41).ToString("O") });
Write(Path.Combine("datasets", "results", "upload-results.json"), results);

// ------------------------------------------------------------------ optional large files
if (largeMb > 0)
{
    var generated = Path.Combine(root, "generated");
    Directory.CreateDirectory(generated);
    var path = Path.Combine(generated, $"large-{largeMb}MB.bin");
    using var stream = File.Create(path);
    var buffer = new byte[1 << 20];
    var random = new Random(42);
    for (var i = 0; i < largeMb; i++)
    {
        random.NextBytes(buffer);
        stream.Write(buffer);
    }

    Console.WriteLine($"Wrote {path}");
}

Console.WriteLine($"Mock data regenerated in {root}");
