using System.IO;
using System.Windows.Media.Imaging;

namespace MigrationKit.Gallery;

public sealed class ThumbnailService
{
    /// <summary>Width the gallery shows thumbnails at.</summary>
    public const int ThumbnailWidth = 160;

    /// <summary>Decoding is CPU-bound; a few at a time keeps the machine responsive.</summary>
    private static readonly int MaxParallelDecodes = Math.Min(4, Environment.ProcessorCount);

    public async Task<IReadOnlyList<GalleryItem>> LoadAsync(IEnumerable<string> paths, CancellationToken cancellationToken = default)
    {
        var list = paths.ToList();
        var items = new GalleryItem[list.Count];

        // Off the UI thread, a few files at a time, and cancellable.
        await Parallel.ForAsync(
            0,
            list.Count,
            new ParallelOptions { MaxDegreeOfParallelism = MaxParallelDecodes, CancellationToken = cancellationToken },
            (index, token) =>
            {
                items[index] = Load(list[index], token);
                return ValueTask.CompletedTask;
            }).ConfigureAwait(false);

        return items;
    }

    private static GalleryItem Load(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = new Uri(path);

        // Decode straight to display size: the full-size pixels are never allocated.
        image.DecodePixelWidth = ThumbnailWidth;

        // OnLoad reads the file during EndInit, so the file is not held open afterwards.
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();

        // Frozen: no thread affinity, no change notifications, cheaper for the render thread.
        // Without this, a bitmap made here could not be shown by the UI thread at all.
        image.Freeze();

        return new GalleryItem(Path.GetFileName(path), image);
    }
}
