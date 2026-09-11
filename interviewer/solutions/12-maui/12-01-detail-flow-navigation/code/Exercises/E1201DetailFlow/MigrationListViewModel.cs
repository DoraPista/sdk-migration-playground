using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MauiGym.Exercises.E1201DetailFlow;

public sealed class MigrationListViewModel : INotifyPropertyChanged
{
    private readonly IMigrationApi _api;
    private readonly IAppNavigation _navigation;
    private bool _isBusy;

    public MigrationListViewModel(IMigrationApi api, IAppNavigation navigation)
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
        try
        {
            var migrations = await _api.GetMigrationsAsync();
            Migrations.Clear();
            foreach (var migration in migrations)
            {
                Migrations.Add(migration);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    public Task OpenAsync(MigrationSummary migration) => _navigation.ShowDetailAsync(migration.Id);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
