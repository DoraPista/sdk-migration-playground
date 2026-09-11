using System.Collections.Concurrent;

namespace Gym.TestUtilities;

/// <summary>
/// A dedicated thread with a single-threaded <see cref="SynchronizationContext"/>, which behaves
/// like the WPF Dispatcher or the MAUI main thread: continuations that capture the context must
/// wait for the thread to be free. Lets tests reproduce UI-thread problems without a UI.
/// </summary>
public sealed class UiThread : IDisposable
{
    private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> _queue = new();
    private readonly Thread _thread;
    private readonly UiSynchronizationContext _context;

    public UiThread()
    {
        _context = new UiSynchronizationContext(this);
        _thread = new Thread(Pump) { IsBackground = true, Name = "Simulated UI thread" };
        _thread.Start();
    }

    public int ManagedThreadId => _thread.ManagedThreadId;

    public bool IsCurrent => Thread.CurrentThread == _thread;

    /// <summary>
    /// Runs <paramref name="action"/> on the UI thread and reports whether it finished in time.
    /// A synchronous call that deadlocks the UI thread returns <c>false</c> instead of hanging the test run.
    /// </summary>
    public bool TryRun(Action action, TimeSpan timeout, out Exception? error)
    {
        var done = new ManualResetEventSlim();
        Exception? captured = null;
        Post(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                captured = ex;
            }
            finally
            {
                done.Set();
            }
        });

        var finished = done.Wait(timeout);
        error = captured;
        return finished;
    }

    /// <summary>Runs an async operation whose continuations resume on the UI thread (like an async event handler).</summary>
    public Task<T> RunAsync<T>(Func<Task<T>> operation)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        Post(async () =>
        {
            try
            {
                tcs.TrySetResult(await operation());
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });
        return tcs.Task;
    }

    public Task RunAsync(Func<Task> operation) => RunAsync(async () =>
    {
        await operation();
        return true;
    });

    public void Post(Action action) => _queue.Add((_ => action(), null));

    public void Dispose() => _queue.CompleteAdding();

    private void Pump()
    {
        SynchronizationContext.SetSynchronizationContext(_context);
        foreach (var (callback, state) in _queue.GetConsumingEnumerable())
        {
            callback(state);
        }
    }

    private sealed class UiSynchronizationContext(UiThread owner) : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state)
        {
            if (!owner._queue.IsAddingCompleted)
            {
                owner._queue.Add((d, state));
            }
        }

        public override void Send(SendOrPostCallback d, object? state)
        {
            if (owner.IsCurrent)
            {
                d(state);
                return;
            }

            using var done = new ManualResetEventSlim();
            Post(s =>
            {
                d(s);
                done.Set();
            }, state);
            done.Wait();
        }

        public override SynchronizationContext CreateCopy() => this;
    }
}
