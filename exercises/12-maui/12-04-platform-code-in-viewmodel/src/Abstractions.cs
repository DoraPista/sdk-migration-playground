namespace MauiGym.Export;

public sealed record PickedFile(string FileName, string FolderPath);

public sealed record ExportResult(bool Succeeded, int Uploaded, string? Error);

/// <summary>The SDK: uploads the chosen files to the migration platform.</summary>
public interface IExportService
{
    Task<ExportResult> UploadAsync(IReadOnlyList<string> files, int maxParallelUploads, CancellationToken cancellationToken = default);
}

// ------------------------------------------------------------------
// The abstractions the team agreed on. The MAUI app implements them with
// FilePicker.Default, Connectivity.Current, Preferences.Default, DisplayAlert and MainThread.
// ------------------------------------------------------------------

public interface IFilePickerService
{
    Task<IReadOnlyList<PickedFile>> PickAsync(CancellationToken cancellationToken = default);
}

public interface IConnectivityService
{
    bool IsOnline { get; }
}

public interface IUserSettings
{
    string? Get(string key);

    void Set(string key, string value);
}

public interface IDialogService
{
    Task ShowAlertAsync(string title, string message);
}

/// <summary>Runs an action on the UI thread (MainThread in the app, inline in tests).</summary>
public interface IUiThread
{
    void Post(Action action);
}

/// <summary>What this device can do. The app decides the values; the view model just uses them.</summary>
public sealed class DeviceCapabilities
{
    public int MaxParallelUploads { get; init; } = 2;
}
