using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MigrationKit.Wpf;

public enum MigrationState
{
    Idle,
    Uploading,
    Verifying,
    Completed,
    Failed,
}

public sealed class MigrationStatusViewModel : INotifyPropertyChanged
{
    private int _filesUploaded;
    private int _filesTotal;
    private MigrationState _state = MigrationState.Idle;

    public int FilesUploaded
    {
        get => _filesUploaded;
        set
        {
            _filesUploaded = value;

            // nameof() instead of a string literal: a typo is now a compile error, not a silent dead binding.
            OnPropertyChanged();
            OnPropertyChanged(nameof(Percent));
            OnPropertyChanged(nameof(StatusText)); // computed properties must be announced too
        }
    }

    public int FilesTotal
    {
        get => _filesTotal;
        set
        {
            _filesTotal = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Percent));
            OnPropertyChanged(nameof(StatusText));
        }
    }

    public MigrationState State
    {
        get => _state;
        set
        {
            _state = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusText));
        }
    }

    public double Percent => FilesTotal == 0 ? 0 : Math.Round(100.0 * FilesUploaded / FilesTotal);

    public string StatusText => State switch
    {
        MigrationState.Idle => "Idle",
        MigrationState.Uploading => $"Uploading {FilesUploaded} of {FilesTotal}…",
        MigrationState.Verifying => "Verifying…",
        MigrationState.Completed => "Migration completed",
        MigrationState.Failed => "Migration failed",
        _ => string.Empty,
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
