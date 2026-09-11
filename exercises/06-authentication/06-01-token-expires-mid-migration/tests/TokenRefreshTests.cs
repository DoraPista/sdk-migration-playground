using System.Net;
using System.Text;
using Gym.TestUtilities;
using MigrationKit.Auth;

namespace Ex0601.Auth.Tests;

public sealed class TokenRefreshTests
{
    private static readonly byte[] Metadata = Encoding.UTF8.GetBytes("""{"project":"PRJ-2001"}""");
    private readonly FakeIdentityPlatform _platform = new();

    [Fact]
    public async Task Uploads_continue_after_the_token_expires()
    {
        var uploader = CreateUploader();
        await uploader.UploadAsync("mig-1", "a.json", Metadata);

        _platform.ExpireAllTokens();
        await uploader.UploadAsync("mig-1", "b.json", Metadata).WithTimeout();

        Assert.Equal(2, _platform.TokenRequests);
    }

    [Fact]
    public async Task First_requests_after_start_up_share_one_token_request()
    {
        var uploader = CreateUploader();

        await Task.WhenAll(Enumerable.Range(1, 8).Select(i => uploader.UploadAsync("mig-1", $"file-{i}.json", Metadata))).WithTimeout();

        Assert.Equal(1, _platform.TokenRequests);
    }

    [Fact]
    public async Task Concurrent_requests_share_one_refresh_when_the_token_expires()
    {
        var uploader = CreateUploader();
        await uploader.UploadAsync("mig-1", "warm-up.json", Metadata);
        _platform.ExpireAllTokens();

        // The identity service is slow: it only answers once all 8 in-flight uploads have been rejected.
        _platform.BeforeIssuingToken = () => Eventually.TrueAsync(
            () => _platform.UnauthorizedResponses >= 8, TimeSpan.FromSeconds(2), "all uploads rejected").ContinueWith(_ => { });

        await Task.WhenAll(Enumerable.Range(1, 8).Select(i => uploader.UploadAsync("mig-1", $"file-{i}.json", Metadata))).WithTimeout();

        Assert.Equal(2, _platform.TokenRequests);
    }

    [Fact]
    public async Task Rejected_fresh_token_fails_fast_instead_of_looping()
    {
        var uploader = CreateUploader();
        _platform.ApiRejectsEveryToken = true;

        await Assert.ThrowsAsync<AuthenticationFailedException>(() => uploader.UploadAsync("mig-1", "a.json", Metadata).WithTimeout());

        Assert.InRange(_platform.TokenRequests, 1, 2);
        Assert.InRange(_platform.Api.CallCount, 1, 2);
    }

    [Fact]
    public async Task Forbidden_is_reported_without_requesting_new_tokens()
    {
        var uploader = CreateUploader();
        _platform.ApiForbidsEverything = true;

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => uploader.UploadAsync("mig-1", "a.json", Metadata).WithTimeout());

        Assert.Equal(HttpStatusCode.Forbidden, error.StatusCode);
        Assert.Equal(1, _platform.TokenRequests);
    }

    [Fact]
    public async Task Revoked_client_credentials_fail_with_AuthenticationFailedException()
    {
        var identity = new ScriptedHttpHandler()
            .RespondJson(HttpStatusCode.OK, new Dictionary<string, object> { ["access_token"] = "tok-1", ["expires_in"] = 3600 })
            .OtherwiseRespond(HttpStatusCode.Unauthorized, new { error = "invalid_client" });
        var api = new ScriptedHttpHandler().RespondJson(HttpStatusCode.Created, new { }).OtherwiseRespond(HttpStatusCode.Unauthorized);
        var uploader = CreateUploader(identity, api);
        await uploader.UploadAsync("mig-1", "a.json", Metadata);

        await Assert.ThrowsAsync<AuthenticationFailedException>(() => uploader.UploadAsync("mig-1", "b.json", Metadata).WithTimeout());
    }

    private FileUploader CreateUploader() => CreateUploader(_platform.Identity, _platform.Api);

    private static FileUploader CreateUploader(HttpMessageHandler identity, HttpMessageHandler api)
    {
        var tokens = new TokenProvider(new HttpClient(identity) { BaseAddress = new Uri("https://login.platform.test/") }, "gym-client", "gym-secret-3f9a1c");
        var platform = new HttpClient(new AuthenticatingHandler(tokens) { InnerHandler = api }) { BaseAddress = new Uri("https://platform.test/") };
        return new FileUploader(platform);
    }
}
