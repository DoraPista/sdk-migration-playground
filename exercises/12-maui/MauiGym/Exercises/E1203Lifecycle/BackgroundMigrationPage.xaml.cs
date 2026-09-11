namespace MauiGym.Exercises.E1203Lifecycle;

public partial class BackgroundMigrationPage : ContentPage
{
    private readonly MigrationRunner _runner;

    public BackgroundMigrationPage()
    {
        InitializeComponent();

        _runner = App.Services.GetRequiredService<MigrationRunner>();
        _runner.Restore();
        _runner.Changed += OnRunnerChanged;
        Show();
    }

    private void OnStartClicked(object? sender, EventArgs e)
    {
        _runner.Start(files: 40);
        Show();
    }

    private void OnRunnerChanged(object? sender, EventArgs e) => Show();

    private void Show()
    {
        ProgressLabel.Text = _runner.Total == 0
            ? "No migration running"
            : $"{_runner.Uploaded} of {_runner.Total} files uploaded";
        Progress.Progress = _runner.Total == 0 ? 0 : (double)_runner.Uploaded / _runner.Total;
        StateFileLabel.Text = $"State file: {_runner.StateFile}";
    }
}
