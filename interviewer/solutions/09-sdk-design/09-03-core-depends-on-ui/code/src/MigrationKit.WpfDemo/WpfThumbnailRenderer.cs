using System.IO;
using System.Windows.Media.Imaging;
using MigrationKit.Core;

namespace MigrationKit.WpfDemo;

/// <summary>The WPF host's imaging: what used to be MigrationKit.Core.ThumbnailService.</summary>
public sealed class WpfThumbnailRenderer : IThumbnailRenderer
{
    public byte[] Render(string imagePath, int maxSize)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri(imagePath);
        bitmap.DecodePixelWidth = maxSize;
        bitmap.CacheOption = BitmapCacheOption.OnLoad; // decode now, then release the file (see 14-02)
        bitmap.EndInit();
        bitmap.Freeze();                               // usable from any thread

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }
}
