using Microsoft.Extensions.DependencyInjection;

namespace MigrationKit.Hosting;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the migration services. The host registers IDestinationDirectory and IFileSink.
    /// Lifetimes follow the data they hold:
    ///   * CustomerSession and UploadTarget hold per-job (per-customer) state → Scoped (one per job scope).
    ///   * A singleton must never depend on them (a captive dependency). Hosts should build the
    ///     provider with ValidateScopes/ValidateOnBuild in development to catch that at startup.
    /// </summary>
    public static IServiceCollection AddMigrationKit(this IServiceCollection services)
    {
        services.AddScoped<CustomerSession>();
        services.AddScoped<UploadTarget>();
        services.AddTransient<MigrationJob>();
        return services;
    }
}

/// <summary>How the desktop app runs a migration job: one DI scope per job.</summary>
public static class JobHost
{
    public static async Task RunJobAsync(IServiceProvider services, string customerId, IReadOnlyList<string> files, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var job = scope.ServiceProvider.GetRequiredService<MigrationJob>();
        await job.RunAsync(customerId, files, cancellationToken);
    }
}
