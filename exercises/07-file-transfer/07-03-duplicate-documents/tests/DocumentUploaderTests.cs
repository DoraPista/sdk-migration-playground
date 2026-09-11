using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Gym.MockServer;
using Gym.TestUtilities;
using MigrationKit.Documents;

namespace Ex0703.Idempotency.Tests;

public sealed class DocumentUploaderTests : IClassFixture<MockServerFixture>
{
    private const string UploadRoute = "POST /migrations/{id}/files";
    private readonly MockServerFixture _server;
    private readonly HttpClient _http;

    public DocumentUploaderTests(MockServerFixture server)
    {
        _server = server;
        _server.Reset();
        _http = new HttpClient { BaseAddress = server.BaseAddress };
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", server.Server.IssueToken());
    }

    [Fact]
    public async Task Document_is_stored_once_on_a_good_network()
    {
        var migrationId = await CreateMigrationAsync();

        await Uploader().UploadAsync(migrationId, Doc("DOC-10231", "Tender.pdf", "tender v1"));

        Assert.Single(Stored(migrationId));
    }

    [Fact]
    public async Task Lost_response_does_not_create_a_duplicate()
    {
        var migrationId = await CreateMigrationAsync();
        _server.Faults.Add(UploadRoute, Fault.ResponseLost());

        await Uploader().UploadAsync(migrationId, Doc("DOC-10231", "Tender.pdf", "tender v1"));

        Assert.Single(Stored(migrationId));
    }

    [Fact]
    public async Task Request_that_never_arrived_is_still_delivered()
    {
        var migrationId = await CreateMigrationAsync();
        _server.Faults.Add(UploadRoute, Fault.DropConnection());

        await Uploader().UploadAsync(migrationId, Doc("DOC-10231", "Tender.pdf", "tender v1"));

        Assert.Single(Stored(migrationId));
    }

    [Fact]
    public async Task Running_the_migration_again_after_a_crash_does_not_duplicate()
    {
        var migrationId = await CreateMigrationAsync();
        var document = Doc("DOC-10231", "Tender.pdf", "tender v1");
        _server.Faults.Add(UploadRoute, Fault.ResponseLost());

        // First run: the response is lost and the app dies before it can retry.
        await Assert.ThrowsAnyAsync<Exception>(() => Uploader(maxAttempts: 1).UploadAsync(migrationId, document));

        // The user starts the app again and re-runs the migration.
        await Uploader().UploadAsync(migrationId, document);

        Assert.Single(Stored(migrationId));
    }

    [Fact]
    public async Task Different_documents_with_the_same_name_are_both_kept()
    {
        var migrationId = await CreateMigrationAsync();

        await Uploader().UploadAsync(migrationId, Doc("DOC-20001", "Report.pdf", "harbour report"));
        await Uploader().UploadAsync(migrationId, Doc("DOC-20002", "Report.pdf", "library report"));

        Assert.Equal(2, Stored(migrationId).Count);
    }

    [Fact]
    public async Task Edited_document_is_uploaded_again()
    {
        var migrationId = await CreateMigrationAsync();

        await Uploader().UploadAsync(migrationId, Doc("DOC-30001", "Plan.pdf", "plan revision A"));
        await Uploader().UploadAsync(migrationId, Doc("DOC-30001", "Plan.pdf", "plan revision B"));

        Assert.Equal(2, Stored(migrationId).Count);
    }

    private IReadOnlyList<StoredFile> Stored(string migrationId) => _server.State.Migrations[migrationId].Files;

    private DocumentUploader Uploader(int maxAttempts = 4) =>
        new(_http, new DocumentUploaderOptions { MaxAttempts = maxAttempts, RetryDelay = TimeSpan.FromMilliseconds(10) });

    private static SourceDocument Doc(string id, string name, string content) => new(id, name, Encoding.UTF8.GetBytes(content));

    private async Task<string> CreateMigrationAsync()
    {
        var response = await _http.PostAsJsonAsync("migrations", new { customerId = "CUST-1002", destinationId = "dst-00012", name = "DMS export" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Created>())!.Id;
    }

    private sealed record Created(string Id);
}
