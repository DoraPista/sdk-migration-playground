using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using MigrationKit.Core;

namespace MigrationKit.WpfDemo;

public partial class MainWindow : Window
{
    private readonly MigrationCoordinator _coordinator;

    // The UI's list belongs to the UI. The core reports events; the window keeps the view state.
    private readonly ObservableCollection<FileRow> _rows = new();

    public MainWindow()
    {
        InitializeComponent();
        _coordinator = new MigrationCoordinator(new DemoUploader(), new WpfThumbnailRenderer());
        FileList.ItemsSource = _rows;

        _coordinator.FileStatusChanged += (_, e) => Dispatcher.Invoke(() => Apply(e));
        _coordinator.ProgressChanged += (_, progress) => Dispatcher.Invoke(() =>
            ProgressText.Text = $"{progress.FilesCompleted}/{progress.FilesTotal} – {Path.GetFileName(progress.CurrentFile)}");
    }

    private void Apply(FileStatusChangedEventArgs e)
    {
        var row = _rows.FirstOrDefault(r => r.Path == e.Path);
        if (row is null)
        {
            _rows.Add(new FileRow(e.Path) { Status = e.Status.ToString() });
            return;
        }

        row.Status = e.Error is null ? e.Status.ToString() : $"{e.Status}: {e.Error}";
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

    private sealed class FileRow(string path) : INotifyPropertyChanged
    {
        private string _status = "Pending";

        public string Path { get; } = path;

        public string Status
        {
            get => _status;
            set
            {
                _status = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
