namespace MauiGym.Exercises.E1201DetailFlow;

/// <summary>
/// Navigation, ported from the WPF app, where showing a screen means replacing the window's content.
/// </summary>
public sealed class LegacyNavigationService
{
    public void ShowDetail(MigrationSummary migration)
    {
        AppState.SelectedMigration = migration;

        var window = Application.Current!.Windows[0];
        window.Page = new NavigationPage(new MigrationDetailPage());
    }

    public void GoBack()
    {
        var window = Application.Current!.Windows[0];
        window.Page = new AppShell();
    }
}
