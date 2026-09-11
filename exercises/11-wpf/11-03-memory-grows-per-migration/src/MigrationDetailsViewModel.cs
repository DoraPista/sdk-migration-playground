using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;

namespace MigrationKit.Wpf;

/// <summary>Shown while the user looks at one migration. Created when the view opens, dropped when it closes.</summary>
public sealed class MigrationDetailsViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly string _migrationId;
    private readonly DispatcherTimer _elapsedTimer;
    private readonly DateTime _openedAt = DateTime.UtcNow;
    private double _percent;
    private string _elapsed = "00:00";
    private bool _closed;

    public MigrationDetailsViewModel(string migrationId, IMigrationMonitor monitor)
    {
        _migrationId = migrationId;
        monitor.ProgressChanged += OnProgressChanged;

        _elapsedTimer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(50) };
        _elapsedTimer.Tick += OnElapsedTick;
        _elapsedTimer.Start();
    }

    public double Percent
    {
        get => _percent;
        private set
        {
            _percent = value;
            OnPropertyChanged();
        }
    }

    public string Elapsed
    {
        get => _elapsed;
        private set
        {
            _elapsed = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Progress events handled since this view model was created (diagnostics).</summary>
    public int HandledEvents { get; private set; }

    public int TimerTicks { get; private set; }

    public void Dispose()
    {
        _closed = true;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnProgressChanged(object? sender, MigrationProgressEventArgs e)
    {
        if (e.MigrationId != _migrationId)
        {
            return;
        }

        HandledEvents++;
        Percent = e.Percent;
    }

    private void OnElapsedTick(object? sender, EventArgs e)
    {
        TimerTicks++;
        Elapsed = (DateTime.UtcNow - _openedAt).ToString(@"mm\:ss");
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
