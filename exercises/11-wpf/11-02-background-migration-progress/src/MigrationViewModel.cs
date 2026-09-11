using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace MigrationKit.Wpf;

public sealed class MigrationViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IMigrationService _service;
    private double _percent;
    private string _status = "Idle";

    public MigrationViewModel(IMigrationService service)
    {
        _service = service;
        _service.FileUploaded += OnFileUploaded;
        _service.ProgressChanged += OnProgressChanged;
    }

    /// <summary>Bound to the file list in the window.</summary>
    public ObservableCollection<string> Files { get; } = new();

    public double Percent
    {
        get => _percent;
        private set
        {
            _percent = value;
            OnPropertyChanged();
        }
    }

    public string Status
    {
        get => _status;
        private set
        {
            _status = value;
            OnPropertyChanged();
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        Status = "Uploading";
        await _service.RunAsync(cancellationToken);
        Status = "Completed";
    }

    public void Dispose()
    {
        _service.FileUploaded -= OnFileUploaded;
        _service.ProgressChanged -= OnProgressChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnFileUploaded(object? sender, FileUploadedEventArgs e)
    {
        Files.Add(e.Name);
    }

    private void OnProgressChanged(object? sender, MigrationProgressEventArgs e)
    {
        Application.Current.Dispatcher.Invoke(() => Percent = e.Percent);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
