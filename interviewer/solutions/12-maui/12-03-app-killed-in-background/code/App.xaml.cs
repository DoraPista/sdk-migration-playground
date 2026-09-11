using MauiGym.Exercises.E1203Lifecycle;

namespace MauiGym;

public partial class App : Application
{
    private readonly IServiceProvider _services;

    public App(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
        Services = services;
    }

    /// <summary>Kept only because other exercises still use it; new code injects what it needs.</summary>
    public static IServiceProvider Services { get; private set; } = null!;

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new AppShell());
        var runner = _services.GetRequiredService<MigrationRunner>();

        // Lifecycle events are a nicety, not a guarantee: Stopped/Destroying may never arrive when the
        // OS kills a backgrounded app. The runner already writes its state after every file; these
        // just flush a little earlier when we do get told.
        window.Stopped += (_, _) => runner.SaveState();
        window.Destroying += (_, _) => runner.SaveState();
        window.Resumed += (_, _) => runner.Restore();

        return window;
    }
}
