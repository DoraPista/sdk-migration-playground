using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MigrationKit.Wpf;

/// <summary>
/// Shows a list of files. It knows nothing about migrations, services, windows or dialogs:
/// the host supplies the data, receives the selection, and decides what "activate" means.
/// </summary>
public partial class FileListControl : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(FileListControl), new PropertyMetadata(null));

    public static readonly DependencyProperty SelectedItemProperty =
        DependencyProperty.Register(
            nameof(SelectedItem),
            typeof(object),
            typeof(FileListControl),
            // Two-way by default: this is what the host binds to, and the user changes it by clicking.
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty ItemActivatedCommandProperty =
        DependencyProperty.Register(nameof(ItemActivatedCommand), typeof(ICommand), typeof(FileListControl), new PropertyMetadata(null));

    public static readonly DependencyProperty NameColumnHeaderProperty =
        DependencyProperty.Register(nameof(NameColumnHeader), typeof(string), typeof(FileListControl), new PropertyMetadata("File"));

    public static readonly DependencyProperty StatusColumnHeaderProperty =
        DependencyProperty.Register(nameof(StatusColumnHeader), typeof(string), typeof(FileListControl), new PropertyMetadata("Status"));

    public FileListControl()
    {
        InitializeComponent();
    }

    /// <summary>Raised when the user activates a row (double-click). Hosts can use this instead of the command.</summary>
    public event EventHandler<object?>? ItemActivated;

    /// <summary>The files to show. Supplied by the host.</summary>
    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <summary>The selected file. Two-way bindable.</summary>
    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    /// <summary>Executed with the row's item when a row is activated.</summary>
    public ICommand? ItemActivatedCommand
    {
        get => (ICommand?)GetValue(ItemActivatedCommandProperty);
        set => SetValue(ItemActivatedCommandProperty, value);
    }

    public string NameColumnHeader
    {
        get => (string)GetValue(NameColumnHeaderProperty);
        set => SetValue(NameColumnHeaderProperty, value);
    }

    public string StatusColumnHeader
    {
        get => (string)GetValue(StatusColumnHeaderProperty);
        set => SetValue(StatusColumnHeaderProperty, value);
    }

    private void OnRowDoubleClick(object sender, MouseButtonEventArgs e)
    {
        var item = Files.SelectedItem;
        if (item is null)
        {
            return;
        }

        ItemActivated?.Invoke(this, item);

        if (ItemActivatedCommand?.CanExecute(item) == true)
        {
            ItemActivatedCommand.Execute(item);
        }
    }
}
