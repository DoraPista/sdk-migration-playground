using System.IO;
using System.Windows;
using MigrationKit.Core;

namespace MigrationKit.WpfDemo;

public partial class MainWindow : Window
{
    private readonly MigrationCoordinator _coordinator;

    public MainWindow()
    {
        InitializeComponent();
        _coordinator = new MigrationCoordinator(new DemoUploader());
        FileList.ItemsSource = _coordinator.Files;
        _coordinator.ProgressChanged += (_, progress) =>
            Dispatcher.Invoke(() => ProgressText.Text = $"{progress.FilesCompleted}/{progress.FilesTotal} – {Path.GetFileName(progress.CurrentFile)}");
    }

    private async void OnStartClicked(object sender, RoutedEventArgs e)
    {
        StartButton.IsEnabled = false;
        try
        {
            var demoFiles = Enumerable.Range(1, 8).Select(i => $@"C:\Projects\Northwind\drawing-{i:D3}.dwg").ToArray();
            var outcome = await _coordinator.RunAsync(demoFiles);
            ProgressText.Text = outcome.Succeeded ? "Migration completed" : "Migration finished with errors";
        }
        finally
        {
            StartButton.IsEnabled = true;
        }
    }

    private sealed class DemoUploader : IFileUploader
    {
        public Task UploadAsync(string path, CancellationToken cancellationToken) => Task.Delay(250, cancellationToken);
    }
}
