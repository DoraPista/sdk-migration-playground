using System.IO;
using System.Windows.Media.Imaging;

namespace MigrationKit.Core;

/// <summary>Creates thumbnails for image files.</summary>
public sealed class ThumbnailService
{
    public BitmapImage CreateThumbnail(string imagePath, int maxSize)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri(imagePath);
        bitmap.DecodePixelWidth = maxSize;
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    public static byte[] ToPngBytes(BitmapSource bitmap)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }
}
