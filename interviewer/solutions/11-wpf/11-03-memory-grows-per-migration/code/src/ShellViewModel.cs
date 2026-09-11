using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MigrationKit.Wpf;

/// <summary>The main window's view model: shows at most one details view at a time.</summary>
public sealed class ShellViewModel : INotifyPropertyChanged
{
    private readonly IMigrationMonitor _monitor;
    private MigrationDetailsViewModel? _details;

    public ShellViewModel(IMigrationMonitor monitor)
    {
        _monitor = monitor;
    }

    public MigrationDetailsViewModel? Details
    {
        get => _details;
        private set
        {
            // Whoever creates a disposable owns it: replacing or clearing the details view disposes the old one.
            _details?.Dispose();
            _details = value;
            OnPropertyChanged();
        }
    }

    public void OpenDetails(string migrationId)
    {
        Details = new MigrationDetailsViewModel(migrationId, _monitor);
    }

    public void CloseDetails()
    {
        Details = null;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
