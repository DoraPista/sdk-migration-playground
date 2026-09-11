using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MigrationKit
{
    // The public surface of MigrationKit 1.4. Partner code compiles against exactly this.

    public interface IMigrationClient
    {
        Task<MigrationHandle> StartAsync(MigrationRequest request, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<ResumableMigration>> GetResumableAsync(CancellationToken cancellationToken = default);
    }

    public class MigrationClient : IMigrationClient, IDisposable
    {
        public MigrationClient(MigrationClientOptions options) => Options = options;

        public MigrationClientOptions Options { get; }

        public Task<MigrationHandle> StartAsync(MigrationRequest request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<ResumableMigration>> GetResumableAsync(CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        /// <summary>Kept for the partner integration that cannot use async.</summary>
        public MigrationResult StartSync(string folder) => throw new NotImplementedException();

        public void Dispose()
        {
        }
    }

    public class MigrationClientOptions
    {
        public Uri PlatformUrl { get; set; } = new Uri("https://migration.contoso-cloud.test/");

        public int MaxConcurrentUploads { get; set; } = 4;

        public string? CheckpointDirectory { get; set; }
    }

    public class MigrationRequest
    {
        public MigrationRequest(string customerId, IEnumerable<MigrationSource> sources)
        {
            CustomerId = customerId;
            Sources = new List<MigrationSource>(sources);
        }

        public string CustomerId { get; }

        public IReadOnlyList<MigrationSource> Sources { get; }

        public string? Name { get; set; }
    }

    public abstract class MigrationSource
    {
        public static MigrationSource FromFolder(string path) => throw new NotImplementedException();
    }

    public sealed class MigrationHandle : IDisposable
    {
        public string MigrationId => throw new NotImplementedException();

        public Task<MigrationResult> Completion => throw new NotImplementedException();

        public event EventHandler<MigrationProgressEventArgs>? ProgressChanged;

        public Task CancelAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public void Dispose() => ProgressChanged = null;
    }

    public enum MigrationStage
    {
        Preparing = 0,
        Validating = 1,
        Uploading = 2,
        Verifying = 3,
        Finishing = 4,
    }

    public enum MigrationStatus
    {
        Succeeded = 0,
        PartiallySucceeded = 1,
        Failed = 2,
        Cancelled = 3,
    }

    public class MigrationProgress
    {
        public MigrationStage Stage { get; set; }

        public int FilesCompleted { get; set; }

        public int FilesTotal { get; set; }

        public long BytesSent { get; set; }

        public long BytesTotal { get; set; }

        public string? CurrentFile { get; set; }
    }

    public class MigrationProgressEventArgs : EventArgs
    {
        public MigrationProgressEventArgs(MigrationProgress progress) => Progress = progress;

        public MigrationProgress Progress { get; }
    }

    public class MigrationResult
    {
        /// <summary>Support uses this to find the run in the platform's logs.</summary>
        public string CorrelationId = string.Empty;

        public MigrationStatus Status { get; set; }

        public int FilesUploaded { get; set; }

        public IReadOnlyList<MigrationFileError> Errors { get; set; } = new List<MigrationFileError>();
    }

    public class MigrationFileError
    {
        public string RelativePath { get; set; } = string.Empty;

        public string Code { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;
    }

    public class ResumableMigration
    {
        public string MigrationId { get; set; } = string.Empty;

        public string CustomerId { get; set; } = string.Empty;

        public DateTimeOffset StartedAt { get; set; }
    }
}
