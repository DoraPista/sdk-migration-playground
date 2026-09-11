using MauiGym.Exercises.E1201DetailFlow;

namespace MauiGym;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute("detail", typeof(MigrationDetailPage));
    }
}
