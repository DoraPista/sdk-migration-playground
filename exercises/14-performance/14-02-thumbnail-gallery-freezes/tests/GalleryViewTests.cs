using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Gym.TestUtilities.Wpf;
using MigrationKit.Gallery;

namespace Ex1402.Gallery.Tests;

public sealed class GalleryViewTests
{
    private const int ProjectPhotos = 2_000;

    [Fact]
    public void The_gallery_only_builds_the_tiles_that_are_on_screen()
    {
        Sta.Run(() =>
        {
            var view = ShowGallery(ProjectPhotos);

            var built = Enumerable.Range(0, ProjectPhotos)
                .Count(i => view.TileList.ItemContainerGenerator.ContainerFromIndex(i) is not null);

            Assert.True(
                built < 200,
                $"{built} of {ProjectPhotos} tiles were built, although only a screenful is visible.");
        });
    }

    [Fact]
    public void Every_tile_shows_its_thumbnail_and_its_name()
    {
        Sta.Run(() =>
        {
            var view = ShowGallery(12);

            var tile = view.TileList.ItemContainerGenerator.ContainerFromIndex(0);
            Assert.NotNull(tile);
            Assert.NotNull(Sta.FindDescendant<Image>(tile!, i => i.Source is not null));
            Assert.NotNull(Sta.FindDescendant<TextBlock>(tile!, t => t.Text == "site-photo-001.png"));
        });
    }

    private static GalleryView ShowGallery(int photos)
    {
        var thumbnail = TinyThumbnail();
        var view = new GalleryView { DataContext = new GalleryStub(photos, thumbnail) };

        Sta.Realize(view, 900, 600);
        return view;
    }

    /// <summary>Stands in for the view model: the tiles matter here, not where they came from.</summary>
    private sealed class GalleryStub
    {
        public GalleryStub(int photos, BitmapSource thumbnail) =>
            Items = Enumerable.Range(1, photos).Select(i => new GalleryItem($"site-photo-{i:000}.png", thumbnail)).ToList();

        public IReadOnlyList<GalleryItem> Items { get; }
    }

    private static BitmapSource TinyThumbnail()
    {
        var bitmap = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgra32, null, new byte[2 * 2 * 4], 2 * 4);
        bitmap.Freeze();
        return bitmap;
    }
}
