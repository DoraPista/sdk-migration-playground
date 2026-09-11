using System.Net;
using Gym.TestUtilities;
using MigrationKit.Diagnostics;

namespace Ex0602.Diagnostics.Tests;

public sealed class SecureLoggingTests
{
    private const string ClientId = "northwind-desktop";
    private const string ClientSecret = "nw-8f3Kq!secret-Zx91";
    private const string AccessToken = "eyJhbGciOiJSUzI1NiJ9.northwind.SIGNATUREabc123";
    private const string SasSignature = "Rk9PQkFSc2lnbmF0dXJlMTIz";
    private static readonly Uri SasUrl = new($"https://northwindmig.blob.core.test/migrations/mig-00102/spec.pdf?sv=2025-05-05&se=2026-09-12T08%3A00%3A00Z&sr=b&sp=cw&sig={SasSignature}");

    private readonly LogCapture _logs = new();

    [Fact]
    public async Task Requesting_a_token_leaks_neither_the_secret_nor_the_token()
    {
        var identity = new ScriptedHttpHandler().RespondJson(HttpStatusCode.OK, new Dictionary<string, object>
        {
            ["access_token"] = AccessToken,
            ["token_type"] = "Bearer",
            ["expires_in"] = 3600,
        });

        var token = await TokenClient(identity).GetTokenAsync();

        Assert.Equal(AccessToken, token);
        AssertNoSecrets(_logs.AllText);
    }

    [Fact]
    public async Task Failed_token_request_does_not_leak_the_secret_in_logs_or_exception()
    {
        var identity = new ScriptedHttpHandler().RespondJson(HttpStatusCode.Unauthorized, new { error = "invalid_client", client_secret_hint = ClientSecret[..6] });

        var error = await Assert.ThrowsAnyAsync<Exception>(() => TokenClient(identity).GetTokenAsync());

        AssertNoSecrets(_logs.AllText);
        AssertNoSecrets(error.ToString());
    }

    [Fact]
    public async Task Authenticated_api_calls_do_not_log_the_bearer_token()
    {
        var api = new ScriptedHttpHandler().OtherwiseRespond(HttpStatusCode.OK, new { status = "Healthy" });
        using var http = new HttpClient(new HttpLoggingHandler(_logs.CreateLogger<HttpLoggingHandler>()) { InnerHandler = api })
        {
            BaseAddress = new Uri("https://platform.test/"),
        };
        http.DefaultRequestHeaders.Authorization = new("Bearer", AccessToken);

        await http.GetAsync("migrations/mig-00102/status");

        AssertNoSecrets(_logs.AllText);
    }

    [Fact]
    public async Task Sas_signature_is_kept_out_of_logs_and_exceptions()
    {
        var storage = new ScriptedHttpHandler().RespondWith(HttpStatusCode.Forbidden);
        var uploader = new BlobUploader(LoggedClient(storage), _logs.CreateLogger<BlobUploader>());

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => uploader.UploadAsync(SasUrl, new MemoryStream(new byte[128])));

        AssertNoSecrets(_logs.AllText);
        AssertNoSecrets(error.ToString());
    }

    [Fact]
    public async Task Logs_remain_useful_for_support()
    {
        var identity = new ScriptedHttpHandler().RespondJson(HttpStatusCode.OK, new Dictionary<string, object> { ["access_token"] = AccessToken, ["expires_in"] = 3600 });
        var storage = new ScriptedHttpHandler().RespondWith(HttpStatusCode.Forbidden);

        await TokenClient(identity).GetTokenAsync();
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            new BlobUploader(LoggedClient(storage), _logs.CreateLogger<BlobUploader>()).UploadAsync(SasUrl, new MemoryStream(new byte[128])));

        var text = _logs.AllText;
        Assert.Contains(ClientId, text);
        Assert.Contains("/migrations/mig-00102/spec.pdf", text);
        Assert.Contains("403", text);
        Assert.Contains("PUT", text);
        Assert.Contains("POST", text);
    }

    private TokenClient TokenClient(ScriptedHttpHandler identity) =>
        new(LoggedClient(identity, "https://login.platform.test/"), new SdkCredentials(ClientId, ClientSecret), _logs.CreateLogger<TokenClient>());

    private HttpClient LoggedClient(ScriptedHttpHandler inner, string? baseAddress = null) =>
        new(new HttpLoggingHandler(_logs.CreateLogger<HttpLoggingHandler>()) { InnerHandler = inner })
        {
            BaseAddress = baseAddress is null ? null : new Uri(baseAddress),
        };

    private static void AssertNoSecrets(string text)
    {
        Assert.DoesNotContain(ClientSecret[..6], text);
        Assert.DoesNotContain("SIGNATUREabc123", text);
        Assert.DoesNotContain(SasSignature, text);
    }
}
