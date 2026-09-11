using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace MigrationKit.Desktop;

public partial class MigrationWindow : Window
{
    private const string ServerUrl = "https://platform.internal.example/api";

    private readonly MigrationRunner _runner = new MigrationRunner();

    private DispatcherTimer _timer;

    private DateTime _startedAt;

    private int _done;

    public MigrationWindow()
    {
        InitializeComponent();

        _runner.FileProgress += OnFileProgress;
        _runner.Finished += OnFinished;
    }

    public async void RunMigration(object sender, RoutedEventArgs e)
    {
        StartButton.IsEnabled = false;
        _startedAt = DateTime.Now;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _timer.Tick += (s, args) => Elapsed.Text = "Elapsed: " + (DateTime.Now - _startedAt).TotalSeconds.ToString("0.0") + " s";
        _timer.Start();

        var folder = FolderBox.Text;
        if (!Directory.Exists(folder))
        {
            MessageBox.Show("Folder not found");
            return;
        }

        var size = GetFolderSize(folder);
        CurrentFile.Text = "Migrating " + size / 1024 / 1024 + " MB";

        try
        {
            await _runner.Run(folder, ApiKeyBox.Text);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Migration failed: " + ex.Message);
        }
    }

    private void OnFileProgress(object sender, FileProgressEventArgs e)
    {
        _done++;
        Progress.Value = _done * 100 / e.Total;
        CurrentFile.Text = "Uploading " + Path.GetFileName(e.FileName);
    }

    private void OnFinished(object sender, EventArgs e)
    {
        MessageBox.Show("Migration finished");
        StartButton.IsEnabled = true;
    }

    /// <summary>Adds up every file under the folder so we can show the total size.</summary>
    private long GetFolderSize(string folder)
    {
        long total = 0;
        foreach (var file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
        {
            total += new FileInfo(file).Length;
        }

        return total;
    }
}
