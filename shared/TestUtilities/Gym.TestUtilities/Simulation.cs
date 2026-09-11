using System.Runtime.CompilerServices;
using Microsoft.Extensions.Time.Testing;

namespace Gym.TestUtilities;

/// <summary>Thrown by test doubles to simulate the process being killed at a precise point.</summary>
public sealed class SimulatedCrashException(string where) : Exception($"Simulated process termination at: {where}");

/// <summary>Counts calls and "kills the process" on the N-th one.</summary>
public sealed class CrashPoint(int crashOnCall, string name = "crash point")
{
    private int _calls;

    public int Calls => Volatile.Read(ref _calls);

    public bool Armed { get; set; } = true;

    public void Hit()
    {
        if (Interlocked.Increment(ref _calls) == crashOnCall && Armed)
        {
            throw new SimulatedCrashException($"{name} (call #{crashOnCall})");
        }
    }
}

/// <summary>
/// Stream wrapper for simulating slow, failing or instrumented reads.
/// </summary>
public sealed class FaultyStream(Stream inner, long failAfterBytes = long.MaxValue) : Stream
{
    private long _position;
    private int _maxSingleRead;

    public long BytesRead => Interlocked.Read(ref _position);

    public int LargestRead => _maxSingleRead;

    public bool WasDisposed { get; private set; }

    /// <summary>Awaited before each async read (e.g. an <see cref="AsyncGate"/> to pause mid-transfer).</summary>
    public Func<CancellationToken, Task>? BeforeRead { get; set; }

    public override bool CanRead => true;
    public override bool CanSeek => inner.CanSeek;
    public override bool CanWrite => false;
    public override long Length => inner.Length;

    public override long Position
    {
        get => inner.Position;
        set => inner.Position = value;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ThrowIfFailing(count);
        var read = inner.Read(buffer, offset, count);
        Track(read);
        return read;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (BeforeRead is not null)
        {
            await BeforeRead(cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfFailing(buffer.Length);
        var read = await inner.ReadAsync(buffer, cancellationToken);
        Track(read);
        return read;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
    public override void Flush() { }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        WasDisposed = true;
        if (disposing)
        {
            inner.Dispose();
        }

        base.Dispose(disposing);
    }

    private void ThrowIfFailing(int requested)
    {
        if (BytesRead >= failAfterBytes && requested > 0)
        {
            throw new IOException($"Simulated read failure after {failAfterBytes} bytes.");
        }
    }

    private void Track(int read)
    {
        Interlocked.Add(ref _position, read);
        if (read > _maxSingleRead)
        {
            _maxSingleRead = read;
        }
    }
}

public static class GcAssert
{
    /// <summary>
    /// True if the object created by <paramref name="create"/> can be garbage-collected once the
    /// test drops its own references. Creation happens in a separate non-inlined frame so the JIT
    /// cannot keep a hidden reference alive.
    /// </summary>
    public static bool IsCollectable(Func<object> create)
    {
        var weak = CreateWeak(create);
        for (var i = 0; i < 3 && weak.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        return !weak.IsAlive;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateWeak(Func<object> create) => new(create());
}

public static class FakeTimeExtensions
{
    /// <summary>
    /// Drives a <see cref="FakeTimeProvider"/> forward until <paramref name="task"/> completes, so code
    /// that waits minutes of (fake) time finishes in milliseconds. Returns the total fake time advanced.
    /// </summary>
    public static async Task<TimeSpan> AdvanceUntilCompleteAsync(
        this FakeTimeProvider time,
        Task task,
        TimeSpan? step = null,
        TimeSpan? maxAdvance = null)
    {
        var increment = step ?? TimeSpan.FromMilliseconds(100);
        var limit = maxAdvance ?? TimeSpan.FromHours(1);
        var advanced = TimeSpan.Zero;
        var realDeadline = DateTime.UtcNow.AddSeconds(30);

        while (!task.IsCompleted)
        {
            // Give the code under test a chance to reach its next timer before moving the clock.
            await Task.Delay(1);
            if (task.IsCompleted)
            {
                break;
            }

            if (advanced >= limit || DateTime.UtcNow > realDeadline)
            {
                throw new TimeoutException($"Task still running after advancing fake time by {advanced}.");
            }

            time.Advance(increment);
            advanced += increment;
        }

        await task;
        return advanced;
    }
}
