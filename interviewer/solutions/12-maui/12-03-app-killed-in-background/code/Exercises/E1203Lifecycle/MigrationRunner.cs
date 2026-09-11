using System.Text.Json;

namespace MauiGym.Exercises.E1203Lifecycle;

/// <summary>
/// Runs a migration while the user keeps using the app.
///
/// Assumptions that do NOT hold on a phone or tablet, and what is done about them:
///   * "We get told before we die."           → Android/iOS can kill a backgrounded process with no callback.
///                                              Progress is therefore written after every file, atomically.
///   * "Task.Run keeps running in background." → The OS suspends the process; work stops (and on iOS, quickly).
///                                              Real background transfer needs a foreground service (Android) or
///                                              URLSession background transfers (iOS); see the interviewer notes.
///   * "The file we wrote is intact."          → A kill during a write leaves a truncated file; write temp + replace.
/// </summary>
public sealed class MigrationRunner
{
    private readonly List<string> _uploaded = new();
    private readonly object _gate = new();
    private int _total;

    public event EventHandler? Changed;

    public bool IsRunning { get; private set; }

    public int Uploaded
    {
        get { lock (_gate) { return _uploaded.Count; } }
    }

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

        Task.Run(async () =>
        {
            for (var i = Uploaded + 1; i <= files; i++)
            {
                await Task.Delay(500);

                lock (_gate)
                {
                    _uploaded.Add($"documents/file-{i:D3}.pdf");
                }

                // Durable after every file: whatever happens next, this file is not uploaded twice
                // and not forgotten. (The platform de-duplicates a repeated upload; see 07-03.)
                SaveState();
                Changed?.Invoke(this, EventArgs.Empty);
            }

            IsRunning = false;
            SaveState();
            Changed?.Invoke(this, EventArgs.Empty);
        });
    }

    /// <summary>Writes progress atomically: a kill mid-write leaves the previous file, never half of one.</summary>
    public void SaveState()
    {
        PersistedState state;
        lock (_gate)
        {
            state = new PersistedState(_total, _uploaded.ToList());
        }

        var temp = StateFile + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(state));
        File.Move(temp, StateFile, overwrite: true);
    }

    public void Restore()
    {
        if (!File.Exists(StateFile))
        {
            return;
        }

        try
        {
            var state = JsonSerializer.Deserialize<PersistedState>(File.ReadAllText(StateFile));
            if (state is null)
            {
                return;
            }

            lock (_gate)
            {
                _total = state.Total;
                _uploaded.Clear();
                _uploaded.AddRange(state.Uploaded);
            }
        }
        catch (JsonException)
        {
            // An unreadable checkpoint must not stop the app: start over and reconcile with the
            // platform instead (see 10-03).
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private sealed record PersistedState(int Total, IReadOnlyList<string> Uploaded);
}
