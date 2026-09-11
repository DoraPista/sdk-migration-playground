namespace MauiGym.Export;

/// <summary>
/// Stands in for the MAUI Essentials statics the screen uses today:
/// <c>FilePicker.Default</c>, <c>Connectivity.Current</c>, <c>Preferences.Default</c>,
/// <c>Page.DisplayAlert</c> and <c>MainThread</c>.
///
/// Like the real ones, these only work inside a running MAUI app; anywhere else they throw.
/// </summary>
public static class PlatformAccess
{
    public static Task<IReadOnlyList<PickedFile>> PickFilesAsync() => throw NotInApp("FilePicker");

    public static bool IsOnline => throw NotInApp("Connectivity");

    public static string? GetPreference(string key) => throw NotInApp("Preferences");

    public static void SetPreference(string key, string value) => throw NotInApp("Preferences");

    public static Task ShowAlertAsync(string title, string message) => throw NotInApp("DisplayAlert");

    public static void OnMainThread(Action action) => throw NotInApp("MainThread");

    private static PlatformNotSupportedException NotInApp(string api) =>
        new($"{api} is only available inside the running MAUI application.");
}
