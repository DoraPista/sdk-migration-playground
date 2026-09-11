using MauiGym.Exercises.E1201DetailFlow;
using MauiGym.Exercises.E1202MigrationList;
using MauiGym.Exercises.E1203Lifecycle;
using Microsoft.Extensions.Logging;

namespace MauiGym;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // ---------------- 12-01 detail flow
        builder.Services.AddSingleton<IMigrationApi, FakeMigrationApi>();
        builder.Services.AddSingleton<LegacyNavigationService>();
        builder.Services.AddTransient<MigrationListPage>();
        builder.Services.AddTransient<MigrationListViewModel>();
        builder.Services.AddSingleton<MigrationDetailViewModel>();

        // ---------------- 12-02 board
        builder.Services.AddTransient<MigrationBoardPage>();
        builder.Services.AddTransient<MigrationBoardViewModel>();

        // ---------------- 12-03 background migration
        builder.Services.AddSingleton<MigrationRunner>();
        builder.Services.AddTransient<BackgroundMigrationPage>();

        return builder.Build();
    }
}
