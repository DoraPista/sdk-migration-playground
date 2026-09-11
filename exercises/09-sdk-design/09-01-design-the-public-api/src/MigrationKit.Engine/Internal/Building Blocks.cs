using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MigrationKit.Internal
{
    // ------------------------------------------------------------------
    // The engine's building blocks. They work (in production they are fully implemented; here the bodies
    // are simplified). Everything is internal: nothing is public API yet.
    // ------------------------------------------------------------------

    internal enum Stage
    {
        Authenticating,
        Provisioning,
        Validating,
        CreatingMigration,
        UploadingMetadata,
        UploadingFiles,
        Verifying,
        Completing,
    }

    internal sealed record SourceItem(string RelativePath, long? Length, Func<CancellationToken, Task<Stream>> OpenRead);

    internal sealed record ValidationIssue(string Code, string Subject, string Message, bool IsBlocking);

    internal sealed record FileFailure(string RelativePath, string ErrorCode, string Message, Exception? Exception);

    /// <summary>Talks to the Migration Platform API (auth, provisioning, migrations, uploads).</summary>
    internal sealed class PlatformClient
    {
        private readonly HttpClient _http;
        private readonly Func<CancellationToken, Task<string>> _getAccessToken;

        public PlatformClient(HttpClient http, Func<CancellationToken, Task<string>> getAccessToken)
        {
            _http = http;
            _getAccessToken = getAccessToken;
        }

        public Task<string> EnsureDestinationAsync(string customerId, string region, CancellationToken cancellationToken) => Task.FromResult("dst-00011");

        public Task<string> CreateMigrationAsync(string customerId, string destinationId, string name, string idempotencyKey, CancellationToken cancellationToken) => Task.FromResult("mig-00001");

        public Task CompleteMigrationAsync(string migrationId, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyCollection<string>> ListStoredFilesAsync(string migrationId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<string>>(Array.Empty<string>());
    }

    /// <summary>Uploads one item (resumable, retried, hashed). Reports bytes as they are sent.</summary>
    internal sealed class FileTransferService
    {
        private readonly PlatformClient _platform;
        private readonly int _maxConcurrency;

        public FileTransferService(PlatformClient platform, int maxConcurrency)
        {
            _platform = platform;
            _maxConcurrency = maxConcurrency;
        }

        public Task UploadAsync(string migrationId, SourceItem item, Action<long> bytesSent, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    /// <summary>Pre-flight checks on the source (missing files, metadata, duplicates, unsafe paths).</summary>
    internal sealed class SourceValidator
    {
        public Task<IReadOnlyList<ValidationIssue>> ValidateAsync(IReadOnlyList<SourceItem> items, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ValidationIssue>>(Array.Empty<ValidationIssue>());
    }

    /// <summary>Durable local state so a migration can be resumed after the process ends.</summary>
    internal sealed class CheckpointStore
    {
        private readonly string _directory;

        public CheckpointStore(string directory) => _directory = directory;

        public Task SaveAsync(string migrationId, object checkpoint, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<string>> ListUnfinishedAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }

    /// <summary>Runs the stages in order and raises low-level callbacks.</summary>
    internal sealed class MigrationWorkflow
    {
        private readonly PlatformClient _platform;
        private readonly FileTransferService _transfer;
        private readonly SourceValidator _validator;
        private readonly CheckpointStore _checkpoints;
        private readonly ILogger _logger;

        public MigrationWorkflow(PlatformClient platform, FileTransferService transfer, SourceValidator validator, CheckpointStore checkpoints, ILogger logger)
        {
            _platform = platform;
            _transfer = transfer;
            _validator = validator;
            _checkpoints = checkpoints;
            _logger = logger;
        }

        public Action<Stage>? StageChanged { get; set; }

        public Action<SourceItem, long>? BytesSent { get; set; }

        public Action<SourceItem>? FileCompleted { get; set; }

        public Action<FileFailure>? FileFailed { get; set; }

        /// <summary>Returns the platform's migration id. Throws on fatal errors.</summary>
        public Task<string> RunAsync(string customerId, IReadOnlyList<SourceItem> items, string? resumeMigrationId, CancellationToken cancellationToken) =>
            Task.FromResult(resumeMigrationId ?? "mig-00001");
    }
}
