using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;

namespace MigrationKit.Wpf;

/// <summary>
/// Bridges an SDK that raises events on worker threads to a UI that may only be touched on its own thread.
///
/// Rules this implements:
///   * Collections bound to an ItemsControl may only be changed on the dispatcher thread.
///     (Scalar properties are marshalled by WPF itself, collections are not.)
///   * The SDK must never wait for the UI: post (BeginInvoke), never Invoke.
///   * The UI is updated at a fixed, modest rate, no matter how fast the events arrive.
/// </summary>
public sealed class MigrationViewModel : INotifyPropertyChanged, IDisposable
{
    private static readonly TimeSpan UiRefreshInterval = TimeSpan.FromMilliseconds(50); // ~20 updates/second

    private readonly IMigrationService _service;
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher; // the thread that created the view model
    private readonly System.Collections.Concurrent.ConcurrentQueue<string> _pendingFiles = new();

    private double _latestPercent;
    private int _flushScheduled;
    private DateTime _lastFlushUtc = DateTime.MinValue;
    private double _percent;
    private string _status = "Idle";

    public MigrationViewModel(IMigrationService service)
    {
        _service = service;
        _service.FileUploaded += OnFileUploaded;
        _service.ProgressChanged += OnProgressChanged;
    }

    /// <summary>Bound to the file list in the window.</summary>
    public ObservableCollection<string> Files { get; } = new();

    public double Percent
    {
        get => _percent;
        private set
        {
            if (Math.Abs(_percent - value) < 0.0001)
            {
                return;
            }

            _percent = value;
            OnPropertyChanged();
        }
    }

    public string Status
    {
        get => _status;
        private set
        {
            _status = value;
            OnPropertyChanged();
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        Status = "Uploading";
        await _service.RunAsync(cancellationToken);

        // Make sure the last batch reaches the UI even if no further events arrive.
        Flush();
        Status = "Completed";
    }

    public void Dispose()
    {
        _service.FileUploaded -= OnFileUploaded;
        _service.ProgressChanged -= OnProgressChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    // ---------------------------------------------------------------- worker thread

    private void OnFileUploaded(object? sender, FileUploadedEventArgs e)
    {
        _pendingFiles.Enqueue(e.Name);
        ScheduleFlush();
    }

    private void OnProgressChanged(object? sender, MigrationProgressEventArgs e)
    {
        Volatile.Write(ref _latestPercent, e.Percent);
        ScheduleFlush();
    }

    /// <summary>Posts at most one pending flush, and at most one per refresh interval.</summary>
    private void ScheduleFlush()
    {
        if (Interlocked.Exchange(ref _flushScheduled, 1) == 1)
        {
            return; // a flush is already on its way; this event will be included in it
        }

        var sinceLast = DateTime.UtcNow - _lastFlushUtc;
        if (sinceLast >= UiRefreshInterval)
        {
            _dispatcher.BeginInvoke(DispatcherPriority.Background, Flush);
        }
        else
        {
            // Too soon: let the timer wake us up instead of burning UI time.
            var timer = new DispatcherTimer(UiRefreshInterval - sinceLast, DispatcherPriority.Background, OnTimerTick, _dispatcher);
            timer.Start();
        }
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        ((DispatcherTimer)sender!).Stop();
        Flush();
    }

    // ---------------------------------------------------------------- UI thread

    private void Flush()
    {
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.BeginInvoke(DispatcherPriority.Background, Flush);
            return;
        }

        Interlocked.Exchange(ref _flushScheduled, 0);
        _lastFlushUtc = DateTime.UtcNow;

        while (_pendingFiles.TryDequeue(out var file))
        {
            Files.Add(file);
        }

        Percent = Volatile.Read(ref _latestPercent);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
