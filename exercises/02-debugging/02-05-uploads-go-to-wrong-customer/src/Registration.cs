using Microsoft.Extensions.DependencyInjection;

namespace MigrationKit.Hosting;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers the migration services. The host registers IDestinationDirectory and IFileSink.</summary>
    public static IServiceCollection AddMigrationKit(this IServiceCollection services)
    {
        services.AddSingleton<CustomerSession>();
        services.AddSingleton<UploadTarget>();
        services.AddTransient<MigrationJob>();
        return services;
    }
}

/// <summary>How the desktop app runs a migration job: one DI scope per job.</summary>
public static class JobHost
{
    public static async Task RunJobAsync(IServiceProvider services, string customerId, IReadOnlyList<string> files, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService<MigrationJob>();
        await job.RunAsync(customerId, files, cancellationToken);
    }
}
