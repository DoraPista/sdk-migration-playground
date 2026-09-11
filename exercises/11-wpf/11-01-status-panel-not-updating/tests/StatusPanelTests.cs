using System.Windows.Controls;
using Gym.TestUtilities.Wpf;
using MigrationKit.Wpf;

namespace Ex1101.StatusPanel.Tests;

/// <summary>Drives the real control on an STA thread, the way the application does.</summary>
public sealed class StatusPanelTests
{
    [Fact]
    public void Panel_shows_the_view_model_it_was_given()
    {
        Sta.Run(() =>
        {
            var viewModel = new MigrationStatusViewModel();

            var panel = Realized(viewModel);

            Assert.Same(viewModel, panel.DataContext);
        });
    }

    [Fact]
    public void Uploaded_file_count_is_shown_while_the_migration_runs()
    {
        Sta.Run(() =>
        {
            var viewModel = new MigrationStatusViewModel { FilesTotal = 20 };
            var panel = Realized(viewModel);

            viewModel.FilesUploaded = 7;
            Sta.DoEvents();

            Assert.Equal("7", TextOf(panel, "FilesText"));
        });
    }

    [Fact]
    public void Status_text_follows_the_migration_state()
    {
        Sta.Run(() =>
        {
            var viewModel = new MigrationStatusViewModel { FilesTotal = 20 };
            var panel = Realized(viewModel);

            viewModel.State = MigrationState.Verifying;
            Sta.DoEvents();

            Assert.Equal("Verifying…", TextOf(panel, "StatusText"));
        });
    }

    [Fact]
    public void Progress_bar_follows_the_percentage()
    {
        Sta.Run(() =>
        {
            var viewModel = new MigrationStatusViewModel { FilesTotal = 20 };
            var panel = Realized(viewModel);

            viewModel.FilesUploaded = 5;
            Sta.DoEvents();

            Assert.Equal(25d, ((ProgressBar)panel.FindName("ProgressBar")!).Value);
        });
    }

    private static MigrationStatusPanel Realized(MigrationStatusViewModel viewModel) =>
        Sta.Realize(new MigrationStatusPanel(viewModel));

    private static string TextOf(MigrationStatusPanel panel, string name) => ((TextBlock)panel.FindName(name)!).Text;
}
