using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MauiGym.Exercises.E1201DetailFlow;

namespace MauiGym.Exercises.E1202MigrationList;

/// <summary>
/// One row of the board. It changes while a migration runs, so it has to raise PropertyChanged:
/// a CollectionView listens to the collection for adds/removes and to each item for property changes.
/// </summary>
public sealed class MigrationRow : INotifyPropertyChanged
{
    private string _state = "Created";
    private int _files;

    public required string Id { get; init; }

    public required string CustomerName { get; init; }

    public string State
    {
        get => _state;
        set
        {
            _state = value;
            OnPropertyChanged();
        }
    }

    public int Files
    {
        get => _files;
        set
        {
            _files = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class MigrationBoardViewModel : INotifyPropertyChanged
{
    private readonly IMigrationApi _api;
    private bool _isRefreshing;

    public MigrationBoardViewModel(IMigrationApi api)
    {
        _api = api;
        RefreshCommand = new Command(async () => await RefreshAsync());
        SimulateProgressCommand = new Command(SimulateProgress);
    }

    /// <summary>Created once and never replaced: the CollectionView is bound to THIS instance.</summary>
    public ObservableCollection<MigrationRow> Migrations { get; } = new();

    public bool IsRefreshing
    {
        get => _isRefreshing;
        set
        {
            _isRefreshing = value;
            OnPropertyChanged();
        }
    }

    public ICommand RefreshCommand { get; }

    public ICommand SimulateProgressCommand { get; }

    public async Task RefreshAsync()
    {
        if (IsRefreshing)
        {
            return;
        }

        IsRefreshing = true;
        try
        {
            // The call itself is async I/O: it does not need a thread of its own, and it does not block the UI.
            var migrations = await _api.GetMigrationsAsync().ConfigureAwait(false);
            var rows = migrations.Select(m => new MigrationRow
            {
                Id = m.Id,
                CustomerName = m.CustomerName,
                State = m.State,
                Files = m.Files,
            }).ToList();

            // Bound collections may only be changed on the main thread (on Android this throws).
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Migrations.Clear();
                foreach (var row in rows)
                {
                    Migrations.Add(row);
                }
            });
        }
        finally
        {
            // Whatever happens, the spinner stops.
            await MainThread.InvokeOnMainThreadAsync(() => IsRefreshing = false);
        }
    }

    /// <summary>Stands in for progress events arriving from the SDK while the board is open.</summary>
    public void SimulateProgress()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            foreach (var row in Migrations)
            {
                row.State = "Uploading";
                row.Files += 10;
            }
        });
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
