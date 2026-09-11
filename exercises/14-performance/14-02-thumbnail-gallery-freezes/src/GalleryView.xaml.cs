using System.Windows.Controls;

namespace MigrationKit.Gallery;

public partial class GalleryView : UserControl
{
    public GalleryView()
    {
        InitializeComponent();
    }

    /// <summary>The panel that holds the tiles. The tests use it to see how much of the list was built.</summary>
    public ItemsControl TileList => Tiles;
}
