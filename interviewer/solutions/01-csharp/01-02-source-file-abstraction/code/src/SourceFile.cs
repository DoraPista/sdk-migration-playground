namespace MigrationKit.Transfer;

/// <summary>
/// Something the SDK can migrate. The SDK never assumes a file-system path exists.
/// </summary>
/// <remarks>
/// Ownership rule: every call to <see cref="OpenReadAsync"/> returns a NEW stream that the caller
/// (the SDK) owns and disposes. Sources must therefore be re-openable: the SDK reads content once
/// to hash it and again to upload it (and again on retry). Nothing is opened until it is needed,
/// so a queue of 10,000 sources holds no handles.
/// </remarks>
public abstract class SourceFile
{
    protected SourceFile(string name, long? length, DateTimeOffset? lastModified, string? contentType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        Length = length;
        LastModified = lastModified;
        ContentType = contentType;
    }

    /// <summary>Display / destination name, e.g. "spec.pdf". Not a path.</summary>
    public string Name { get; }

    /// <summary>Null when the source cannot know its length up front (some picker streams).</summary>
    public long? Length { get; }

    public DateTimeOffset? LastModified { get; }

    public string? ContentType { get; }

    public abstract Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default);

    public static SourceFile FromPath(string path) => new LocalSourceFile(path);

    public static SourceFile FromBytes(string name, ReadOnlyMemory<byte> content, string? contentType = null) =>
        new InMemorySourceFile(name, content, contentType);

    /// <summary>
    /// For sources that can only hand out streams (MAUI FileResult, zip entries, cloud pickers...).
    /// The factory must return a fresh stream each time it is called.
    /// </summary>
    public static SourceFile FromStreamFactory(
        string name,
        Func<CancellationToken, Task<Stream>> openRead,
        long? length = null,
        DateTimeOffset? lastModified = null,
        string? contentType = null) =>
        new DelegateSourceFile(name, openRead, length, lastModified, contentType);
}

internal sealed class LocalSourceFile : SourceFile
{
    private readonly string _path;

    public LocalSourceFile(string path)
        : this(new FileInfo(path))
    {
    }

    private LocalSourceFile(FileInfo info)
        : base(info.Name, info.Exists ? info.Length : null, info.Exists ? info.LastWriteTimeUtc : null, null)
    {
        _path = info.FullName;
    }

    public override Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<Stream>(new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan));
}

internal sealed class InMemorySourceFile(string name, ReadOnlyMemory<byte> content, string? contentType)
    : SourceFile(name, content.Length, null, contentType)
{
    public override Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<Stream>(new MemoryStream(content.ToArray(), writable: false));
}

internal sealed class DelegateSourceFile(
    string name,
    Func<CancellationToken, Task<Stream>> openRead,
    long? length,
    DateTimeOffset? lastModified,
    string? contentType) : SourceFile(name, length, lastModified, contentType)
{
    public override Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default) => openRead(cancellationToken);
}
