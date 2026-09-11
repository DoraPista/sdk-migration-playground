using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Ex1402.Gallery.Tests;

/// <summary>Writes real PNG files (1,600 × 1,200, like a site photo) into a temp folder.</summary>
public sealed class SampleImages : IDisposable
{
    public const int SourceWidth = 1600;
    public const int SourceHeight = 1200;

    private readonly string _root = Path.Combine(Path.GetTempPath(), "gym-gallery-" + Guid.NewGuid().ToString("N"));

    /// <summary>A project's worth of photos; enough for the memory question, few enough to stay fast.</summary>
    public const int Count = 24;

    public SampleImages()
    {
        Directory.CreateDirectory(_root);
        Paths = Enumerable.Range(1, Count).Select(Write).ToList();
    }

    public IReadOnlyList<string> Paths { get; }

    private string Write(int index)
    {
        var path = Path.Combine(_root, $"site-photo-{index:000}.png");

        // A gradient, so the PNG does not compress to nothing and decoding has real work to do.
        var stride = SourceWidth * 4;
        var pixels = new byte[stride * SourceHeight];
        for (var y = 0; y < SourceHeight; y++)
        {
            for (var x = 0; x < SourceWidth; x++)
            {
                var i = (y * stride) + (x * 4);
                pixels[i] = (byte)(x + index);
                pixels[i + 1] = (byte)(y + index);
                pixels[i + 2] = (byte)(x ^ y);
                pixels[i + 3] = 255;
            }
        }

        var bitmap = BitmapSource.Create(SourceWidth, SourceHeight, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        using var file = File.Create(path);
        encoder.Save(file);
        return path;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // A thumbnail may still hold the file; the temp folder is cleaned up by the OS.
        }
    }
}
