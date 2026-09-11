using System.Net;
using System.Text;
using Gym.TestUtilities;
using Microsoft.Extensions.Time.Testing;
using MigrationKit.Status;

namespace Ex0502.Status.Tests;

public sealed class MigrationStatusClientTests
{
    private static readonly TimeSpan Poll = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Status_is_read()
    {
        var server = new VersionedPlatform(v1States: ["Uploading"]);

        var status = await Client(server).GetStatusAsync("mig-00102");

        Assert.Equal(MigrationState.Uploading, status.State);
        Assert.Equal(40, status.FilesReceived);
    }

    [Fact]
    public async Task Client_asks_for_the_api_version_it_was_built_against()
    {
        var server = new VersionedPlatform(v1States: ["Uploading", "Completed"]);
        var time = new FakeTimeProvider();

        var waiting = Client(server, time).WaitForCompletionAsync("mig-00102", Poll);
        await time.AdvanceUntilCompleteAsync(waiting, Poll);

        Assert.Equal(MigrationState.Completed, (await waiting).State);
        Assert.All(server.Handler.Requests, r => Assert.Equal("1", r.Header("api-version")));
    }

    [Fact]
    public async Task New_response_properties_do_not_break_the_client()
    {
        var server = new VersionedPlatform(v1States: ["Uploading"], extraProperties: true);

        var status = await Client(server).GetStatusAsync("mig-00102");

        Assert.Equal(MigrationState.Uploading, status.State);
    }

    [Fact]
    public async Task Unrecognised_state_is_not_fatal_and_is_not_mistaken_for_completion()
    {
        var server = new VersionedPlatform(v1States: ["Paused"]);

        var status = await Client(server).GetStatusAsync("mig-00102");

        Assert.NotEqual(MigrationState.Completed, status.State);
    }

    [Fact]
    public async Task Waiting_continues_through_unrecognised_states()
    {
        var server = new VersionedPlatform(v1States: ["Uploading", "Paused", "Paused", "Completed"], extraProperties: true);
        var time = new FakeTimeProvider();

        var waiting = Client(server, time).WaitForCompletionAsync("mig-00102", Poll);
        await time.AdvanceUntilCompleteAsync(waiting, Poll);

        Assert.Equal(MigrationState.Completed, (await waiting).State);
        Assert.Equal(4, server.Handler.CallCount);
    }

    private static MigrationStatusClient Client(VersionedPlatform server, TimeProvider? time = null) =>
        new(new HttpClient(server.Handler) { BaseAddress = new Uri("https://platform.test/") }, time);

    /// <summary>
    /// Serves version 1 to clients that ask for it and the latest version (2) to everyone else,
    /// like the real platform after Monday's release.
    /// </summary>
    private sealed class VersionedPlatform
    {
        private readonly Queue<string> _v1States;
        private readonly bool _extraProperties;

        public VersionedPlatform(IEnumerable<string> v1States, bool extraProperties = false)
        {
            _v1States = new Queue<string>(v1States);
            _extraProperties = extraProperties;
            Handler = new ScriptedHttpHandler().Otherwise((request, _) => Task.FromResult(Respond(request)));
        }

        public ScriptedHttpHandler Handler { get; }

        private HttpResponseMessage Respond(HttpRequestMessage request)
        {
            var askedForV1 = request.Headers.TryGetValues("api-version", out var values) && values.Contains("1");
            var json = askedForV1
                ? V1(_v1States.Count > 1 ? _v1States.Dequeue() : _v1States.Peek())
                : """{"id":"mig-00102","state":"Verifying","progress":{"filesReceived":40,"bytesReceived":81234567},"updatedAt":"2026-09-07T08:15:00+00:00","estimatedCompletion":"2026-09-07T09:00:00+00:00"}""";
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }

        private string V1(string state) => _extraProperties
            ? $$"""{"id":"mig-00102","state":"{{state}}","filesReceived":40,"bytesReceived":81234567,"updatedAt":"2026-09-07T08:15:00+00:00","estimatedCompletion":"2026-09-07T09:00:00+00:00","region":"westeurope"}"""
            : $$"""{"id":"mig-00102","state":"{{state}}","filesReceived":40,"bytesReceived":81234567,"updatedAt":"2026-09-07T08:15:00+00:00"}""";
    }
}
