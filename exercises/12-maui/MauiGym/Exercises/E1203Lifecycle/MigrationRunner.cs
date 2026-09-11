using System.Text.Json;

namespace MauiGym.Exercises.E1203Lifecycle;

/// <summary>Runs a migration in the background while the user keeps using the app.</summary>
public sealed class MigrationRunner
{
    private readonly List<string> _uploaded = new();
    private int _total;

    public event EventHandler? Changed;

    public bool IsRunning { get; private set; }

    public int Uploaded => _uploaded.Count;

    public int Total => _total;

    public string StateFile => Path.Combine(FileSystem.AppDataDirectory, "migration-state.json");

    public void Start(int files)
    {
        if (IsRunning)
        {
            return;
        }

        _total = files;
        IsRunning = true;

        // The migration keeps running while the user browses other tabs.
        Task.Run(async () =>
        {
            for (var i = _uploaded.Count + 1; i <= files; i++)
            {
                await Task.Delay(500);
                _uploaded.Add($"documents/file-{i:D3}.pdf");
                Changed?.Invoke(this, EventArgs.Empty);
            }

            IsRunning = false;
            Changed?.Invoke(this, EventArgs.Empty);
        });
    }

    /// <summary>Writes the progress so the next start can continue. Called when the window is destroyed.</summary>
    public void SaveState()
    {
        var state = new PersistedState(_total, _uploaded);
        File.WriteAllText(StateFile, JsonSerializer.Serialize(state));
    }

    public void Restore()
    {
        if (!File.Exists(StateFile))
        {
            return;
        }

        var state = JsonSerializer.Deserialize<PersistedState>(File.ReadAllText(StateFile));
        if (state is null)
        {
            return;
        }

        _total = state.Total;
        _uploaded.Clear();
        _uploaded.AddRange(state.Uploaded);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private sealed record PersistedState(int Total, IReadOnlyList<string> Uploaded);
}
