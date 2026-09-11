using Microsoft.Extensions.DependencyInjection;
using MigrationKit.Hosting;

namespace Ex0205.Hosting.Tests;

/// <summary>Guards against re-introducing captive dependencies.</summary>
public sealed class RegistrationValidationTests
{
    [Fact]
    public void Registrations_pass_scope_validation()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDestinationDirectory>(new NullDirectory());
        services.AddSingleton<IFileSink>(new NullSink());
        services.AddMigrationKit();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<MigrationJob>());
    }

    private sealed class NullDirectory : IDestinationDirectory
    {
        public Task<string> GetDestinationAsync(string customerId, CancellationToken cancellationToken) => Task.FromResult("dst");
    }

    private sealed class NullSink : IFileSink
    {
        public Task UploadAsync(string destinationId, string customerId, string file, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
