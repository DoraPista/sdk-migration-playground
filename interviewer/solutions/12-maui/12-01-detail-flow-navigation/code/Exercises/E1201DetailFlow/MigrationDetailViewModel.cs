using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MauiGym.Exercises.E1201DetailFlow;

/// <summary>
/// Receives the migration through Shell navigation parameters instead of a static.
/// IQueryAttributable runs before OnAppearing, so the page can start loading immediately.
/// </summary>
public sealed class MigrationDetailViewModel : INotifyPropertyChanged, IQueryAttributable
{
    private readonly IMigrationApi _api;
    private MigrationDetail? _detail;
    private string? _migrationId;

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

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        // An id (not the object) survives deep links and Android process death.
        _migrationId = query.TryGetValue("id", out var id) ? id?.ToString() : null;
        Detail = null;
    }

    public async Task LoadAsync()
    {
        if (_migrationId is null)
        {
            return;
        }

        Detail = await _api.GetMigrationAsync(_migrationId);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
