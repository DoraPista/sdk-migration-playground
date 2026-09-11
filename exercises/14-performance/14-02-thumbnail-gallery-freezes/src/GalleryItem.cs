using System.Windows.Media.Imaging;

namespace MigrationKit.Gallery;

/// <summary>One tile in the gallery.</summary>
public sealed class GalleryItem
{
    public GalleryItem(string name, BitmapSource thumbnail)
    {
        Name = name;
        Thumbnail = thumbnail;
    }

    public string Name { get; }

    public BitmapSource Thumbnail { get; }
}
