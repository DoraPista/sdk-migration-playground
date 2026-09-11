using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MauiGym.Export;

/// <summary>
/// Everything the platform provides arrives through an abstraction, so this class is plain C#:
/// it runs in a unit test, on Windows, on Android, and in a future host nobody has thought of yet.
/// </summary>
public sealed class ExportViewModel : INotifyPropertyChanged
{
    private const string LastFolderKey = "export.last-folder";

    private readonly IExportService _exports;
    private readonly IFilePickerService _filePicker;
    private readonly IConnectivityService _connectivity;
    private readonly IUserSettings _settings;
    private readonly IDialogService _dialogs;
    private readonly IUiThread _uiThread;
    private readonly DeviceCapabilities _capabilities;
    private string _status = "Ready";

    public ExportViewModel(
        IExportService exports,
        IFilePickerService filePicker,
        IConnectivityService connectivity,
        IUserSettings settings,
        IDialogService dialogs,
        IUiThread uiThread,
        DeviceCapabilities capabilities)
    {
        _exports = exports;
        _filePicker = filePicker;
        _connectivity = connectivity;
        _settings = settings;
        _dialogs = dialogs;
        _uiThread = uiThread;
        _capabilities = capabilities;
    }

    public ObservableCollection<string> SelectedFiles { get; } = new();

    public string Status
    {
        get => _status;
        private set
        {
            _status = value;
            OnPropertyChanged();
        }
    }

    public string? LastFolder => _settings.Get(LastFolderKey);

    public async Task ChooseFilesAsync()
    {
        var picked = await _filePicker.PickAsync();
        if (picked.Count == 0)
        {
            return; // the user cancelled the picker
        }

        SelectedFiles.Clear();
        foreach (var file in picked)
        {
            SelectedFiles.Add(file.FileName);
        }

        _settings.Set(LastFolderKey, picked[0].FolderPath);
        Status = $"{picked.Count} file(s) selected";
    }

    public async Task StartExportAsync(CancellationToken cancellationToken = default)
    {
        if (!_connectivity.IsOnline)
        {
            await _dialogs.ShowAlertAsync("No connection", "Connect to a network and try again.");
            return;
        }

        if (SelectedFiles.Count == 0)
        {
            await _dialogs.ShowAlertAsync("Nothing selected", "Choose the files to export first.");
            return;
        }

        Status = "Uploading…";

        // How much this device can do is a platform decision, made where the platform is known (MauiProgram),
        // not with #if in the logic.
        var result = await _exports.UploadAsync(SelectedFiles.ToList(), _capabilities.MaxParallelUploads, cancellationToken);

        _uiThread.Post(() => Status = result.Succeeded ? $"Uploaded {result.Uploaded} file(s)" : "Upload failed");

        if (!result.Succeeded)
        {
            await _dialogs.ShowAlertAsync("Upload failed", result.Error ?? "Unknown error");
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
