using System.Windows.Controls;
using Gym.TestUtilities.Wpf;
using MigrationKit.Gallery;

namespace Ex1402.Gallery.Tests;

public sealed class ThumbnailServiceTests : IClassFixture<SampleImages>
{
    private readonly SampleImages _images;

    public ThumbnailServiceTests(SampleImages images)
    {
        _images = images;
    }

    [Fact]
    public async Task Thumbnails_are_decoded_at_the_size_they_are_shown()
    {
        var items = await new ThumbnailService().LoadAsync(_images.Paths.Take(4));

        Assert.All(items, item =>
        {
            Assert.True(
                item.Thumbnail.PixelWidth <= ThumbnailService.ThumbnailWidth,
                $"{item.Name} was decoded at {item.Thumbnail.PixelWidth}×{item.Thumbnail.PixelHeight}.");
            Assert.True(item.Thumbnail.PixelHeight > 0);
        });
    }

    [Fact]
    public async Task The_aspect_ratio_is_kept()
    {
        var items = await new ThumbnailService().LoadAsync(_images.Paths.Take(1));

        var thumbnail = items[0].Thumbnail;
        var expected = (double)SampleImages.SourceHeight / SampleImages.SourceWidth;
        Assert.Equal(expected, (double)thumbnail.PixelHeight / thumbnail.PixelWidth, 1);
    }

    [Fact]
    public async Task All_the_thumbnails_of_a_project_together_stay_small()
    {
        var items = await new ThumbnailService().LoadAsync(_images.Paths);

        var bytes = items.Sum(i => (long)i.Thumbnail.PixelWidth * i.Thumbnail.PixelHeight * 4);
        Assert.True(bytes < 8 * 1024 * 1024, $"{items.Count} thumbnails hold {bytes / (1024 * 1024)} MB of pixels.");
    }

    /// <summary>
    /// The gallery loads away from the UI thread, so the thumbnails must be usable on it.
    /// An unfrozen bitmap belongs to the thread that made it and throws when it is shown.
    /// </summary>
    [Fact]
    public async Task Thumbnails_made_off_the_user_interface_thread_can_be_shown_on_it()
    {
        var items = await Task.Run(() => new ThumbnailService().LoadAsync(_images.Paths.Take(2)));

        Sta.Run(() =>
        {
            var image = new Image { Source = items[0].Thumbnail };
            Sta.Realize(image, 160, 120);

            Assert.True(items[0].Thumbnail.IsFrozen, "Thumbnails must be frozen before they leave the loading thread.");
        });
    }

    [Fact]
    public async Task Closing_the_gallery_stops_the_loading()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new ThumbnailService().LoadAsync(_images.Paths, cts.Token));
    }
}
