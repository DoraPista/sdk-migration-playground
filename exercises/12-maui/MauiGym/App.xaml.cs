using MauiGym.Exercises.E1203Lifecycle;

namespace MauiGym;

public partial class App : Application
{
    public App(IServiceProvider services)
    {
        InitializeComponent();

        // The whole app reaches for services through here when constructor injection "doesn't fit".
        Services = services;
    }

    /// <summary>Service locator used by pages that are created with `new`.</summary>
    public static IServiceProvider Services { get; private set; } = null!;

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new AppShell());

        // The migration's progress is written to disk when the window goes away.
        window.Destroying += (_, _) => Services.GetRequiredService<MigrationRunner>().SaveState();

        return window;
    }
}
