using System.Reflection;
using System.Runtime.Versioning;
using MigrationKit.Core;

namespace MigrationKit.Core.Tests;

/// <summary>Keeps the core usable by every host. These run in seconds and fail the build if the core regresses.</summary>
public sealed class ArchitectureTests
{
    private static readonly string[] ForbiddenAssemblies =
    [
        "PresentationFramework", "PresentationCore", "WindowsBase", "System.Windows.Forms", "System.Xaml", "System.Drawing",
    ];

    [Fact]
    public void Core_does_not_reference_a_ui_framework()
    {
        var referenced = typeof(MigrationCoordinator).Assembly.GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .ToArray();

        var offending = referenced
            .Where(name => ForbiddenAssemblies.Contains(name, StringComparer.OrdinalIgnoreCase) || name.StartsWith("Microsoft.Maui", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.True(offending.Length == 0, $"MigrationKit.Core references: {string.Join(", ", offending)}");
    }

    [Fact]
    public void Core_is_not_tied_to_one_operating_system()
    {
        var platform = typeof(MigrationCoordinator).Assembly.GetCustomAttribute<TargetPlatformAttribute>();

        Assert.True(platform is null, $"MigrationKit.Core targets {platform?.PlatformName}; hosts on other platforms cannot reference it.");
    }
}
