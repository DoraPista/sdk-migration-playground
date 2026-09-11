using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MauiGym.Exercises.E1201DetailFlow;

public sealed class MigrationDetailViewModel : INotifyPropertyChanged
{
    private readonly IMigrationApi _api;
    private MigrationDetail? _detail;

    public MigrationDetailViewModel(IMigrationApi api)
    {
        _api = api;
    }

    public MigrationDetail? Detail
    {
        get => _detail;
        private set
        {
            _detail = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Title));
        }
    }

    public string Title => Detail?.CustomerName ?? "Loading…";

    public async Task LoadAsync()
    {
        var selected = AppState.SelectedMigration;
        if (selected is null)
        {
            return;
        }

        Detail = await _api.GetMigrationAsync(selected.Id);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
