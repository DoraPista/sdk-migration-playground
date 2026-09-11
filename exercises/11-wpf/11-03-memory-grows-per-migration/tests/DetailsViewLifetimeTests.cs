using Gym.TestUtilities;
using Gym.TestUtilities.Wpf;
using MigrationKit.Wpf;

namespace Ex1103.Leaks.Tests;

public sealed class DetailsViewLifetimeTests
{
    [Fact]
    public void An_open_details_view_follows_the_migration()
    {
        Sta.Run(() =>
        {
            var monitor = new MigrationMonitor();
            var shell = new ShellViewModel(monitor);
            shell.OpenDetails("mig-00102");

            monitor.Report("mig-00102", 42);

            Assert.Equal(42, shell.Details!.Percent);
            Assert.Equal(1, shell.Details.HandledEvents);
            Sta.PumpUntil(() => shell.Details!.TimerTicks > 0, because: "the elapsed-time timer to tick");
        });
    }

    [Fact]
    public void A_closed_details_view_stops_reacting_to_the_monitor()
    {
        Sta.Run(() =>
        {
            var monitor = new MigrationMonitor();
            var shell = new ShellViewModel(monitor);
            shell.OpenDetails("mig-00102");
            var closed = shell.Details!;
            monitor.Report("mig-00102", 10);

            shell.CloseDetails();
            monitor.Report("mig-00102", 20);
            monitor.Report("mig-00102", 30);

            Assert.Equal(1, closed.HandledEvents);
            Assert.Equal(0, monitor.SubscriberCount);
        });
    }

    [Fact]
    public void A_closed_details_view_stops_its_timer()
    {
        Sta.Run(() =>
        {
            var monitor = new MigrationMonitor();
            var shell = new ShellViewModel(monitor);
            shell.OpenDetails("mig-00102");
            var closed = shell.Details!;
            Sta.PumpUntil(() => closed.TimerTicks > 0, because: "the first tick");

            shell.CloseDetails();
            var ticksAtClose = closed.TimerTicks;
            Sta.DoEvents();
            Thread.Sleep(200);
            Sta.DoEvents();

            Assert.Equal(ticksAtClose, closed.TimerTicks);
        });
    }

    [Fact]
    public void Opening_and_closing_many_details_views_leaves_nothing_behind()
    {
        Sta.Run(() =>
        {
            var monitor = new MigrationMonitor();
            var shell = new ShellViewModel(monitor);

            for (var i = 0; i < 20; i++)
            {
                shell.OpenDetails($"mig-{i:D5}");
                monitor.Report($"mig-{i:D5}", 50);
                shell.CloseDetails();
            }

            Assert.Equal(0, monitor.SubscriberCount);
        });
    }

    [Fact]
    public void A_closed_details_view_can_be_garbage_collected()
    {
        Sta.Run(() =>
        {
            var monitor = new MigrationMonitor();
            var shell = new ShellViewModel(monitor);

            var collectable = GcAssert.IsCollectable(() =>
            {
                shell.OpenDetails("mig-00102");
                var details = shell.Details!;
                shell.CloseDetails();
                Sta.DoEvents(); // let any queued dispatcher work finish before we look for references
                return details;
            });

            Assert.True(collectable, "A closed details view model is still reachable (monitor event or dispatcher timer).");
            GC.KeepAlive(monitor);
        });
    }
}
