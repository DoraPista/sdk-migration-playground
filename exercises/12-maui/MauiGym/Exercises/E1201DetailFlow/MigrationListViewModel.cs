using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MauiGym.Exercises.E1201DetailFlow;

public sealed class MigrationListViewModel : INotifyPropertyChanged
{
    private readonly IMigrationApi _api;
    private readonly LegacyNavigationService _navigation;
    private bool _isBusy;

    public MigrationListViewModel(IMigrationApi api, LegacyNavigationService navigation)
    {
        _api = api;
        _navigation = navigation;
    }

    public ObservableCollection<MigrationSummary> Migrations { get; } = new();

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            _isBusy = value;
            OnPropertyChanged();
        }
    }

    public async Task LoadAsync()
    {
        IsBusy = true;
        Migrations.Clear();
        foreach (var migration in await _api.GetMigrationsAsync())
        {
            Migrations.Add(migration);
        }

        IsBusy = false;
    }

    public void Open(MigrationSummary migration) => _navigation.ShowDetail(migration);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
