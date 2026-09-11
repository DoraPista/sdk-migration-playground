namespace MauiGym.Exercises.E1201DetailFlow;

/// <summary>
/// Navigation through Shell: routes, parameters, and the platform's own back behaviour.
/// (Kept behind an interface so view models stay testable and don't touch Shell directly.)
/// </summary>
public interface IAppNavigation
{
    Task ShowDetailAsync(string migrationId);

    Task GoBackAsync();
}

public sealed class ShellNavigation : IAppNavigation
{
    public Task ShowDetailAsync(string migrationId) =>
        Shell.Current.GoToAsync("detail", new Dictionary<string, object> { ["id"] = migrationId });

    public Task GoBackAsync() => Shell.Current.GoToAsync("..");
}

/// <summary>Kept so existing call sites compile; it now delegates to Shell navigation.</summary>
public sealed class LegacyNavigationService(IAppNavigation navigation)
{
    public Task ShowDetail(MigrationSummary migration) => navigation.ShowDetailAsync(migration.Id);

    public Task GoBack() => navigation.GoBackAsync();
}
