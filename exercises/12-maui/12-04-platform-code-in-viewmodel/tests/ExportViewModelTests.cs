using Gym.TestUtilities;
using MauiGym.Export;

namespace Ex1204.Export.Tests;

/// <summary>Plain unit tests: no device, no emulator, no MAUI app.</summary>
public sealed class ExportViewModelTests
{
    private readonly FakePicker _picker = new();
    private readonly FakeConnectivity _connectivity = new();
    private readonly FakeSettings _settings = new();
    private readonly FakeDialogs _dialogs = new();
    private readonly FakeUiThread _uiThread = new();
    private readonly FakeExportService _exports = new();

    [Fact]
    public async Task Choosing_files_fills_the_selection_and_remembers_the_folder()
    {
        _picker.Files =
        [
            new PickedFile("specification.pdf", @"C:\Projects\Northwind\documents"),
            new PickedFile("deck-inspection-001.jpg", @"C:\Projects\Northwind\images"),
        ];

        var viewModel = ViewModel();

        await viewModel.ChooseFilesAsync().WithTimeout();

        Assert.Equal(["specification.pdf", "deck-inspection-001.jpg"], viewModel.SelectedFiles);
        Assert.Equal(@"C:\Projects\Northwind\documents", _settings.Get("export.last-folder"));
    }

    [Fact]
    public async Task Exporting_without_a_connection_warns_and_uploads_nothing()
    {
        _connectivity.IsOnline = false;
        var viewModel = ViewModel();
        _picker.Files = [new PickedFile("a.pdf", "folder")];
        await viewModel.ChooseFilesAsync();

        await viewModel.StartExportAsync().WithTimeout();

        Assert.Contains(_dialogs.Shown, d => d.Title == "No connection");
        Assert.Empty(_exports.Uploads);
    }

    [Fact]
    public async Task Exporting_nothing_warns_and_uploads_nothing()
    {
        var viewModel = ViewModel();

        await viewModel.StartExportAsync().WithTimeout();

        Assert.Contains(_dialogs.Shown, d => d.Title == "Nothing selected");
        Assert.Empty(_exports.Uploads);
    }

    [Fact]
    public async Task Successful_export_uploads_the_selection_with_the_device_limit()
    {
        _picker.Files = [new PickedFile("a.pdf", "folder"), new PickedFile("b.pdf", "folder")];
        var viewModel = ViewModel(new DeviceCapabilities { MaxParallelUploads = 8 });
        await viewModel.ChooseFilesAsync();

        await viewModel.StartExportAsync().WithTimeout();

        var upload = Assert.Single(_exports.Uploads);
        Assert.Equal(["a.pdf", "b.pdf"], upload.Files);
        Assert.Equal(8, upload.MaxParallelUploads);
        Assert.Equal("Uploaded 2 file(s)", viewModel.Status);
        Assert.Empty(_dialogs.Shown);
    }

    [Fact]
    public async Task A_failed_export_is_shown_to_the_user()
    {
        _picker.Files = [new PickedFile("a.pdf", "folder")];
        _exports.Result = new ExportResult(false, 0, "The platform rejected the migration (403).");
        var viewModel = ViewModel();
        await viewModel.ChooseFilesAsync();

        await viewModel.StartExportAsync().WithTimeout();

        Assert.Equal("Upload failed", viewModel.Status);
        Assert.Contains(_dialogs.Shown, d => d.Title == "Upload failed" && d.Message.Contains("403"));
    }

    [Fact]
    public async Task Status_updates_are_posted_to_the_user_interface_thread()
    {
        _picker.Files = [new PickedFile("a.pdf", "folder")];
        var viewModel = ViewModel();
        await viewModel.ChooseFilesAsync();

        await viewModel.StartExportAsync().WithTimeout();

        Assert.True(_uiThread.Posts > 0, "The view model updated UI state without going through IUiThread.");
    }

    private ExportViewModel ViewModel(DeviceCapabilities? capabilities = null) =>
        new(_exports, _picker, _connectivity, _settings, _dialogs, _uiThread, capabilities ?? new DeviceCapabilities());

    private sealed class FakePicker : IFilePickerService
    {
        public IReadOnlyList<PickedFile> Files { get; set; } = [];

        public Task<IReadOnlyList<PickedFile>> PickAsync(CancellationToken cancellationToken = default) => Task.FromResult(Files);
    }

    private sealed class FakeConnectivity : IConnectivityService
    {
        public bool IsOnline { get; set; } = true;
    }

    private sealed class FakeSettings : IUserSettings
    {
        private readonly Dictionary<string, string> _values = new();

        public string? Get(string key) => _values.TryGetValue(key, out var value) ? value : null;

        public void Set(string key, string value) => _values[key] = value;
    }

    private sealed record ShownDialog(string Title, string Message);

    private sealed class FakeDialogs : IDialogService
    {
        private readonly List<ShownDialog> _shown = new();

        public IReadOnlyList<ShownDialog> Shown => _shown;

        public Task ShowAlertAsync(string title, string message)
        {
            _shown.Add(new ShownDialog(title, message));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUiThread : IUiThread
    {
        public int Posts { get; private set; }

        public void Post(Action action)
        {
            Posts++;
            action();
        }
    }

    private sealed record UploadCall(IReadOnlyList<string> Files, int MaxParallelUploads);

    private sealed class FakeExportService : IExportService
    {
        private readonly List<UploadCall> _uploads = new();

        public IReadOnlyList<UploadCall> Uploads => _uploads;

        public ExportResult? Result { get; set; }

        public Task<ExportResult> UploadAsync(IReadOnlyList<string> files, int maxParallelUploads, CancellationToken cancellationToken = default)
        {
            _uploads.Add(new UploadCall(files, maxParallelUploads));
            return Task.FromResult(Result ?? new ExportResult(true, files.Count, null));
        }
    }
}
