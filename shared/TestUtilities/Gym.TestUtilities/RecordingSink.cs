using System.Security.Cryptography;
using System.Text;

namespace Gym.TestUtilities;

/// <summary>
/// A write-only stream that hashes and counts everything written to it and keeps the first 64 KB
/// (for small bodies such as JSON). Used to record request bodies without buffering them.
/// </summary>
internal sealed class RecordingSink : Stream
{
    private const int KeepBytes = 64 * 1024;
    private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private readonly MemoryStream _head = new();

    public long BytesWritten { get; private set; }

    public string Sha256 => Convert.ToHexStringLower(_hash.GetCurrentHash());

    public string? Text => BytesWritten <= KeepBytes ? Encoding.UTF8.GetString(_head.GetBuffer(), 0, (int)_head.Length) : null;

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => BytesWritten;
        set => throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        _hash.AppendData(buffer);
        var room = KeepBytes - (int)_head.Length;
        if (room > 0)
        {
            _head.Write(buffer[..Math.Min(room, buffer.Length)]);
        }

        BytesWritten += buffer.Length;
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Write(buffer, offset, count);
        return Task.CompletedTask;
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Write(buffer.Span);
        return ValueTask.CompletedTask;
    }

    public override void Flush()
    {
    }

    public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hash.Dispose();
            _head.Dispose();
        }

        base.Dispose(disposing);
    }
}
