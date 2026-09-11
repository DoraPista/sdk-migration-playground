using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MigrationKit.Wpf;

public partial class FileListControl : UserControl
{
    public FileListControl()
    {
        InitializeComponent();

        // Load the migration's files as early as possible so the list is filled when the window appears.
        Files.ItemsSource = MigrationService.Instance.GetFilesAsync().Result;
    }

    /// <summary>The files to show.</summary>
    public IEnumerable? ItemsSource { get; set; }

    /// <summary>The selected file.</summary>
    public object? SelectedItem { get; set; }

    /// <summary>Executed when a row is activated.</summary>
    public ICommand? ItemActivatedCommand { get; set; }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SelectedItem = Files.SelectedItem;
    }

    private void OnRowDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (Files.SelectedItem is not FileItem file)
        {
            MessageBox.Show("Select a file first.", "Migration");
            return;
        }

        ((MainWindow)Application.Current.MainWindow).ShowDetails(file);
    }
}

/// <summary>The migration app's main window (the control talks to it directly).</summary>
public class MainWindow : Window
{
    public FileItem? LastShown { get; private set; }

    public void ShowDetails(FileItem file) => LastShown = file;
}
