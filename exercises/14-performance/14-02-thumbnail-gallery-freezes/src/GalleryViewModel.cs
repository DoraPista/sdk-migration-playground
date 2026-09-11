using System.Collections.ObjectModel;

namespace MigrationKit.Gallery;

public sealed class GalleryViewModel
{
    private readonly ThumbnailService _thumbnails;

    public GalleryViewModel(ThumbnailService thumbnails)
    {
        _thumbnails = thumbnails;
    }

    public ObservableCollection<GalleryItem> Items { get; } = [];

    public async Task ShowAsync(IEnumerable<string> paths, CancellationToken cancellationToken = default)
    {
        Items.Clear();

        foreach (var item in await _thumbnails.LoadAsync(paths, cancellationToken))
        {
            Items.Add(item);
        }
    }
}
