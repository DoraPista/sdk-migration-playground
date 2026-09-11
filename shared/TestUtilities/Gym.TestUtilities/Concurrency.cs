namespace Gym.TestUtilities;

/// <summary>
/// A gate that holds callers until the test opens it. Lets tests create an exact interleaving
/// ("both requests are in flight now") instead of relying on timing.
/// </summary>
public sealed class AsyncGate
{
    private readonly TaskCompletionSource _open = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _waiting;

    public int Waiting => Volatile.Read(ref _waiting);

    public bool IsOpen => _open.Task.IsCompleted;

    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _waiting);
        await _open.Task.WaitAsync(cancellationToken);
    }

    public void Open() => _open.TrySetResult();

    /// <summary>Waits (with a safety timeout) until at least <paramref name="count"/> callers are blocked.</summary>
    public Task WhenWaitingAsync(int count, TimeSpan? timeout = null) =>
        Eventually.TrueAsync(() => Waiting >= count, timeout ?? TimeSpan.FromSeconds(10), $"expected {count} waiter(s), saw {Waiting}");
}

/// <summary>Tracks how many operations run at once.</summary>
public sealed class ConcurrencyProbe
{
    private int _current;
    private int _peak;
    private int _total;

    public int Current => Volatile.Read(ref _current);

    public int Peak => Volatile.Read(ref _peak);

    public int Total => Volatile.Read(ref _total);

    public IDisposable Enter()
    {
        Interlocked.Increment(ref _total);
        var now = Interlocked.Increment(ref _current);
        int peak;
        while (now > (peak = Volatile.Read(ref _peak)) && Interlocked.CompareExchange(ref _peak, now, peak) != peak)
        {
        }

        return new Exit(this);
    }

    private sealed class Exit(ConcurrencyProbe probe) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                Interlocked.Decrement(ref probe._current);
            }
        }
    }
}

public static class Eventually
{
    /// <summary>
    /// Polls a condition. Only used to wait for something the test itself set up (never to "wait long enough");
    /// the timeout is a safety net that turns a hang into a readable failure.
    /// </summary>
    public static async Task TrueAsync(Func<bool> condition, TimeSpan timeout, string because)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"Condition not met within {timeout.TotalSeconds:0.#}s: {because}");
            }

            await Task.Delay(5);
        }
    }
}

public static class TaskTestExtensions
{
    /// <summary>Fails the test (instead of hanging the run) if the task does not finish in time.</summary>
    public static async Task WithTimeout(this Task task, TimeSpan? timeout = null, string? because = null)
    {
        var limit = timeout ?? TimeSpan.FromSeconds(10);
        var finished = await Task.WhenAny(task, Task.Delay(limit));
        if (finished != task)
        {
            throw new TimeoutException($"Operation did not complete within {limit.TotalSeconds:0.#}s. {because}".Trim());
        }

        await task;
    }

    public static async Task<T> WithTimeout<T>(this Task<T> task, TimeSpan? timeout = null, string? because = null)
    {
        await ((Task)task).WithTimeout(timeout, because);
        return await task;
    }
}
