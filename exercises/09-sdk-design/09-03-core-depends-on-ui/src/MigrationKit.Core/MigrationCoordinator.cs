using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace MigrationKit.Core;

/// <summary>One row of the migration's file list.</summary>
public sealed class FileItem : INotifyPropertyChanged
{
    private string _status = "Pending";

    public FileItem(string path) => Path = path;

    public string Path { get; }

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

/// <summary>Runs a migration: uploads the files, tracks their state and reports progress.</summary>
public sealed class MigrationCoordinator
{
    private static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png"];

    private readonly IFileUploader _uploader;
    private readonly ThumbnailService _thumbnails = new();

    public MigrationCoordinator(IFileUploader uploader, IThumbnailRenderer? thumbnailRenderer = null)
    {
        _uploader = uploader;
    }

    /// <summary>The file list the desktop app binds to.</summary>
    public ObservableCollection<FileItem> Files { get; } = new();

    public event EventHandler<MigrationProgress>? ProgressChanged;

    public async Task<MigrationOutcome> RunAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken = default)
    {
        var outcomes = new List<FileOutcome>();

        foreach (var path in paths)
        {
            var item = new FileItem(path);
            Application.Current.Dispatcher.Invoke(() => Files.Add(item));

            try
            {
                await _uploader.UploadAsync(path, cancellationToken);
                item.Status = "Uploaded";

                byte[]? thumbnail = null;
                if (IsImage(path))
                {
                    thumbnail = ThumbnailService.ToPngBytes(_thumbnails.CreateThumbnail(path, 128));
                }

                outcomes.Add(new FileOutcome(path, true, null, thumbnail));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not migrate {System.IO.Path.GetFileName(path)}:\n{ex.Message}", "Migration");
                item.Status = "Failed";
                outcomes.Add(new FileOutcome(path, false, ex.Message, null));
            }

            var progress = new MigrationProgress(outcomes.Count(o => o.Succeeded), paths.Count, path);
            Application.Current.Dispatcher.BeginInvoke(() => ProgressChanged?.Invoke(this, progress));
        }

        return new MigrationOutcome(outcomes.All(o => o.Succeeded), outcomes);
    }

    private static bool IsImage(string path) =>
        ImageExtensions.Contains(System.IO.Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
}
