using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MauiGym.Exercises.E1201DetailFlow;

namespace MauiGym.Exercises.E1202MigrationList;

/// <summary>One row of the board.</summary>
public sealed class MigrationRow
{
    public required string Id { get; init; }

    public required string CustomerName { get; init; }

    public string State { get; set; } = "Created";

    public int Files { get; set; }
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

    public ObservableCollection<MigrationRow> Migrations { get; private set; } = new();

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
        IsRefreshing = true;

        await Task.Run(async () =>
        {
            var migrations = await _api.GetMigrationsAsync();
            Migrations = new ObservableCollection<MigrationRow>(migrations.Select(m => new MigrationRow
            {
                Id = m.Id,
                CustomerName = m.CustomerName,
                State = m.State,
                Files = m.Files,
            }));
        });
    }

    /// <summary>Stands in for progress events arriving from the SDK while the board is open.</summary>
    public void SimulateProgress()
    {
        foreach (var row in Migrations)
        {
            row.State = "Uploading";
            row.Files += 10;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
