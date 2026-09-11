using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MauiGym.Export;

public sealed class ExportViewModel : INotifyPropertyChanged
{
    private const string LastFolderKey = "export.last-folder";

    private readonly IExportService _exports;
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

    public string? LastFolder => PlatformAccess.GetPreference(LastFolderKey);

    public async Task ChooseFilesAsync()
    {
        var picked = await PlatformAccess.PickFilesAsync();
        if (picked.Count == 0)
        {
            return;
        }

        SelectedFiles.Clear();
        foreach (var file in picked)
        {
            SelectedFiles.Add(file.FileName);
        }

        PlatformAccess.SetPreference(LastFolderKey, picked[0].FolderPath);
        Status = $"{picked.Count} file(s) selected";
    }

    public async Task StartExportAsync(CancellationToken cancellationToken = default)
    {
        if (!PlatformAccess.IsOnline)
        {
            await PlatformAccess.ShowAlertAsync("No connection", "Connect to a network and try again.");
            return;
        }

        if (SelectedFiles.Count == 0)
        {
            await PlatformAccess.ShowAlertAsync("Nothing selected", "Choose the files to export first.");
            return;
        }

#if WINDOWS
        var maxParallelUploads = 8;
#else
        var maxParallelUploads = 2; // mobile data
#endif

        Status = "Uploading…";
        var result = await _exports.UploadAsync(SelectedFiles.ToList(), maxParallelUploads, cancellationToken);

        PlatformAccess.OnMainThread(() => Status = result.Succeeded ? $"Uploaded {result.Uploaded} file(s)" : "Upload failed");

        if (!result.Succeeded)
        {
            await PlatformAccess.ShowAlertAsync("Upload failed", result.Error ?? "Unknown error");
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
