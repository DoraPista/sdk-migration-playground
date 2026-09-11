using System.IO;
using System.Windows.Media.Imaging;

namespace MigrationKit.Gallery;

public sealed class ThumbnailService
{
    /// <summary>Width the gallery shows thumbnails at.</summary>
    public const int ThumbnailWidth = 160;

    public Task<IReadOnlyList<GalleryItem>> LoadAsync(IEnumerable<string> paths, CancellationToken cancellationToken = default)
    {
        var items = new List<GalleryItem>();

        foreach (var path in paths)
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(path);
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();

            items.Add(new GalleryItem(Path.GetFileName(path), image));
        }

        return Task.FromResult<IReadOnlyList<GalleryItem>>(items);
    }
}
