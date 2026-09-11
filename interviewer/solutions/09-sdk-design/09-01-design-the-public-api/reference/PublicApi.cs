// ---------------------------------------------------------------------------------------------------
// A REFERENCE public API for MigrationKit (one good answer, not the only one).
// Not compiled as part of the repository; it is here so the interviewer has something concrete to compare with.
// Targets .NET Standard 2.0: no records with positional syntax across the public surface (they work with a
// polyfill, but plain classes evolve more safely), no IAsyncEnumerable in the core flow, no default interface methods.
// ---------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MigrationKit
{
    // ----------------------------------------------------------------- entry point

    /// <summary>Creates and monitors migrations. One instance can serve several migrations; it is thread-safe.</summary>
    public sealed class MigrationClient : IDisposable
    {
        public MigrationClient(MigrationClientOptions options) { }

        /// <summary>Checks the source before a migration is started. Never throws for data problems.</summary>
        public Task<ValidationReport> ValidateAsync(MigrationRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        /// <summary>
        /// Starts a migration and returns a handle. The migration runs until it completes, fails, or is cancelled.
        /// </summary>
        public Task<MigrationHandle> StartAsync(MigrationRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        /// <summary>Migrations that were interrupted (app closed, crash) and can be resumed.</summary>
        public Task<IReadOnlyList<ResumableMigration>> GetResumableAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<MigrationHandle> ResumeAsync(string migrationId, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public void Dispose() { }
    }

    /// <summary>A running migration: progress, completion and cancellation for one job.</summary>
    public sealed class MigrationHandle : IDisposable
    {
        /// <summary>The platform's migration id. Stable across resumes; put it in support tickets.</summary>
        public string MigrationId => throw new NotImplementedException();

        /// <summary>Completes when the migration reaches a terminal state. Does not throw for migration failures.</summary>
        public Task<MigrationResult> Completion => throw new NotImplementedException();

        /// <summary>
        /// Raised as the migration progresses. Raised on a background thread: UI hosts must marshal
        /// (WPF: Dispatcher; MAUI: MainThread). Events are coalesced to at most a few per second.
        /// </summary>
        public event EventHandler<MigrationProgressEventArgs> ProgressChanged
        {
            add { }
            remove { }
        }

        /// <summary>Requests cancellation. <see cref="Completion"/> then finishes with <see cref="MigrationStatus.Cancelled"/>.</summary>
        public Task CancelAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public void Dispose() { }
    }

    // ----------------------------------------------------------------- inputs

    /// <summary>What to migrate. Built by the host; validated by the SDK.</summary>
    public sealed class MigrationRequest
    {
        public MigrationRequest(string customerId, IEnumerable<MigrationSource> sources) { }

        public string CustomerId => throw new NotImplementedException();

        public IReadOnlyList<MigrationSource> Sources => throw new NotImplementedException();

        /// <summary>Shown in the platform's portal. Optional.</summary>
        public string? Name { get; set; }
    }

    /// <summary>Where content comes from. Hosts can add their own kinds (MAUI picker, zip, DMS).</summary>
    public abstract class MigrationSource
    {
        public static MigrationSource FromFolder(string path, bool includeSubfolders = true) => throw new NotImplementedException();

        public static MigrationSource FromFiles(IEnumerable<string> paths) => throw new NotImplementedException();

        public static MigrationSource FromStream(string name, Func<CancellationToken, Task<System.IO.Stream>> openRead, long? length = null) => throw new NotImplementedException();
    }

    // ----------------------------------------------------------------- configuration

    public sealed class MigrationClientOptions
    {
        /// <summary>The platform to migrate to.</summary>
        public Uri PlatformUrl { get; set; } = new Uri("https://migration.contoso-cloud.test/");

        /// <summary>Supplies access tokens. The host decides where secrets live.</summary>
        public IMigrationCredential Credential { get; set; } = null!;

        /// <summary>Where resume information is stored. Defaults to a per-user application-data folder.</summary>
        public string? CheckpointDirectory { get; set; }

        public int MaxConcurrentUploads { get; set; } = 4;

        public int MaxConcurrentMigrations { get; set; } = 3;

        public TimeSpan ProgressInterval { get; set; } = TimeSpan.FromMilliseconds(250);

        /// <summary>The host's logging. Null = no logging.</summary>
        public ILoggerFactory? LoggerFactory { get; set; }

        /// <summary>Lets the host supply its own HttpMessageHandler (proxies, certificates, instrumentation).</summary>
        public Func<System.Net.Http.HttpMessageHandler>? HttpMessageHandlerFactory { get; set; }
    }

    /// <summary>Implemented by the host: Windows Credential Manager, interactive sign-in, managed identity …</summary>
    public interface IMigrationCredential
    {
        Task<AccessToken> GetTokenAsync(CancellationToken cancellationToken);
    }

    public readonly struct AccessToken
    {
        public AccessToken(string token, DateTimeOffset expiresOn) { Token = token; ExpiresOn = expiresOn; }

        public string Token { get; }

        public DateTimeOffset ExpiresOn { get; }
    }

    // ----------------------------------------------------------------- progress and results

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

    /// <summary>A snapshot of a migration's progress. Immutable; safe to hand to a UI thread.</summary>
    public sealed class MigrationProgress
    {
        public MigrationStage Stage { get; }
        public int FilesCompleted { get; }
        public int FilesTotal { get; }
        public long BytesSent { get; }
        public long BytesTotal { get; }
        public string? CurrentFile { get; }
        public int FailedFiles { get; }
    }

    public sealed class MigrationProgressEventArgs : EventArgs
    {
        public MigrationProgress Progress => throw new NotImplementedException();
    }

    public sealed class MigrationResult
    {
        public string MigrationId => throw new NotImplementedException();
        public MigrationStatus Status => throw new NotImplementedException();
        public int FilesUploaded => throw new NotImplementedException();
        public long BytesUploaded => throw new NotImplementedException();
        public IReadOnlyList<MigrationFileError> Errors => throw new NotImplementedException();
        public TimeSpan Duration => throw new NotImplementedException();

        /// <summary>An id support can use to find this run in the platform's logs.</summary>
        public string CorrelationId => throw new NotImplementedException();
    }

    public sealed class MigrationFileError
    {
        public string RelativePath => throw new NotImplementedException();
        public string Code => throw new NotImplementedException();       // stable, machine-readable
        public string Message => throw new NotImplementedException();    // for the user
        public bool CanRetry => throw new NotImplementedException();
    }

    public sealed class ValidationReport
    {
        public bool CanStart => throw new NotImplementedException();
        public IReadOnlyList<ValidationIssue> Issues => throw new NotImplementedException();
        public int FileCount => throw new NotImplementedException();
        public long TotalBytes => throw new NotImplementedException();
    }

    public sealed class ValidationIssue
    {
        public string Code => throw new NotImplementedException();
        public string Subject => throw new NotImplementedException();
        public string Message => throw new NotImplementedException();
        public bool IsBlocking => throw new NotImplementedException();
    }

    public sealed class ResumableMigration
    {
        public string MigrationId => throw new NotImplementedException();
        public string CustomerId => throw new NotImplementedException();
        public DateTimeOffset StartedAt => throw new NotImplementedException();
        public int FilesRemaining => throw new NotImplementedException();
    }

    // ----------------------------------------------------------------- errors

    /// <summary>Base class for every exception the SDK throws. Callers can catch just this.</summary>
    public class MigrationException : Exception
    {
        public MigrationException(string message, Exception? innerException = null) : base(message, innerException) { }

        public string? CorrelationId { get; }
    }

    /// <summary>The SDK could not authenticate with the credential the host supplied.</summary>
    public sealed class MigrationAuthenticationException : MigrationException
    {
        public MigrationAuthenticationException(string message, Exception? innerException = null) : base(message, innerException) { }
    }

    /// <summary>The request was rejected by the platform and cannot succeed as-is.</summary>
    public sealed class MigrationRequestException : MigrationException
    {
        public MigrationRequestException(string message, Exception? innerException = null) : base(message, innerException) { }

        public int? StatusCode { get; }
    }
}

// ---------------------------------------------------------------------------------------------------
// Host usage (WPF view model)
// ---------------------------------------------------------------------------------------------------
//
//  _client = new MigrationClient(new MigrationClientOptions
//  {
//      PlatformUrl = settings.PlatformUrl,
//      Credential = new CredentialManagerCredential(),   // host-owned
//      LoggerFactory = App.LoggerFactory,
//      MaxConcurrentUploads = settings.UploadThreads,
//  });
//
//  var report = await _client.ValidateAsync(request);
//  if (!report.CanStart) { ShowIssues(report.Issues); return; }
//
//  _handle = await _client.StartAsync(request, ct);
//  _handle.ProgressChanged += (_, e) => _dispatcher.Post(() => Apply(e.Progress));  // host marshals
//
//  var result = await _handle.Completion;
//  Status = result.Status switch { ... };
//
//  // Cancel button:  await _handle.CancelAsync();
