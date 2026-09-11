namespace MauiGym.Exercises.E1201DetailFlow;

public sealed record MigrationSummary(string Id, string CustomerName, string State, int Files, long Bytes);

public sealed record MigrationDetail(string Id, string CustomerName, string State, IReadOnlyList<string> RecentFiles);

public interface IMigrationApi
{
    Task<IReadOnlyList<MigrationSummary>> GetMigrationsAsync(CancellationToken cancellationToken = default);

    Task<MigrationDetail> GetMigrationAsync(string id, CancellationToken cancellationToken = default);
}

/// <summary>Stands in for the platform API so the gym runs offline.</summary>
public sealed class FakeMigrationApi : IMigrationApi
{
    private static readonly MigrationSummary[] Migrations =
    [
        new("mig-00101", "Northwind Architects", "Completed", 1_180, 4_100_000_000),
        new("mig-00102", "Northwind Architects", "Uploading", 690, 2_300_000_000),
        new("mig-00103", "Contoso Civil Engineering", "Failed", 12, 90_000_000),
        new("mig-00104", "Fabrikam Surveying GmbH", "Created", 0, 0),
    ];

    public async Task<IReadOnlyList<MigrationSummary>> GetMigrationsAsync(CancellationToken cancellationToken = default)
    {
        await Task.Delay(400, cancellationToken); // the platform is not instant
        return Migrations;
    }

    public async Task<MigrationDetail> GetMigrationAsync(string id, CancellationToken cancellationToken = default)
    {
        await Task.Delay(300, cancellationToken);
        var migration = Migrations.FirstOrDefault(m => m.Id == id) ?? Migrations[0];
        return new MigrationDetail(
            migration.Id,
            migration.CustomerName,
            migration.State,
            [$"documents/specification.pdf", "images/deck-inspection-001.jpg", "survey/points.csv"]);
    }
}
