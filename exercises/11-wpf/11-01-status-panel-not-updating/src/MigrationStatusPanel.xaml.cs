using System.Windows.Controls;

namespace MigrationKit.Wpf;

public partial class MigrationStatusPanel : UserControl
{
    /// <param name="viewModel">The view model for the migration this panel shows (one per migration).</param>
    public MigrationStatusPanel(MigrationStatusViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
