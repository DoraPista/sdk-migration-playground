using System.Collections.Concurrent;
using Gym.TestUtilities;
using MigrationKit.Core;

namespace MigrationKit.Core.Tests;

/// <summary>
/// These tests run like the nightly console service: no Application, no dispatcher, no window.
/// </summary>
public sealed class HeadlessMigrationTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);
    private static readonly string[] Files = [@"C:\Projects\a.dwg", @"C:\Projects\site-photo.jpg", @"C:\Projects\b.dwg"];

    [Fact]
    public async Task Migration_runs_without_a_user_interface()
    {
        var progress = new ConcurrentQueue<MigrationProgress>();
        var coordinator = new MigrationCoordinator(new FakeUploader(), new FakeRenderer());
        coordinator.ProgressChanged += (_, p) => progress.Enqueue(p);

        var outcome = await coordinator.RunAsync(Files).WithTimeout(Patience);

        Assert.True(outcome.Succeeded);
        Assert.Equal(3, outcome.Files.Count);
        Assert.Equal(3, progress.Count);
        Assert.Equal(3, progress.Last().FilesCompleted);
    }

    [Fact]
    public async Task Thumbnails_are_produced_by_the_host_renderer()
    {
        var renderer = new FakeRenderer();
        var coordinator = new MigrationCoordinator(new FakeUploader(), renderer);

        var outcome = await coordinator.RunAsync(Files).WithTimeout(Patience);

        Assert.Equal([@"C:\Projects\site-photo.jpg"], renderer.Rendered);
        var photo = outcome.Files.Single(f => f.Path.EndsWith("site-photo.jpg"));
        Assert.Equal(FakeRenderer.Png, photo.Thumbnail);
        Assert.All(outcome.Files.Where(f => !f.Path.EndsWith(".jpg")), f => Assert.Null(f.Thumbnail));
    }

    [Fact]
    public async Task A_failed_file_is_reported_in_the_result_and_not_shown_in_a_dialog()
    {
        var coordinator = new MigrationCoordinator(new FakeUploader(failOn: @"C:\Projects\b.dwg"), new FakeRenderer());

        var outcome = await coordinator.RunAsync(Files).WithTimeout(Patience, "the core is waiting for someone to close a dialog");

        Assert.False(outcome.Succeeded);
        var failed = outcome.Files.Single(f => !f.Succeeded);
        Assert.Equal(@"C:\Projects\b.dwg", failed.Path);
        Assert.Contains("network path", failed.Error);
        Assert.Equal(2, outcome.Files.Count(f => f.Succeeded));
    }

    private sealed class FakeUploader(string? failOn = null) : IFileUploader
    {
        public async Task UploadAsync(string path, CancellationToken cancellationToken)
        {
            await Task.Yield();
            if (path == failOn)
            {
                throw new IOException(@"The network path \\fs02\projects was not found.");
            }
        }
    }

    private sealed class FakeRenderer : IThumbnailRenderer
    {
        public static readonly byte[] Png = [0x89, (byte)'P', (byte)'N', (byte)'G', 1, 2, 3];

        private readonly ConcurrentQueue<string> _rendered = new();

        public IReadOnlyList<string> Rendered => _rendered.ToArray();

        public byte[] Render(string imagePath, int maxSize)
        {
            _rendered.Enqueue(imagePath);
            return Png;
        }
    }
}
