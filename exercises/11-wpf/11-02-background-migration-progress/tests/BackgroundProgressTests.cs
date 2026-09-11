using System.ComponentModel;
using System.Windows.Controls;
using Gym.TestUtilities.Wpf;
using MigrationKit.Wpf;

namespace Ex1102.Progress.Tests;

public sealed class BackgroundProgressTests
{
    [Fact]
    public void Files_appear_in_the_bound_list_while_the_migration_runs()
    {
        Sta.RunAsync(async () =>
        {
            var service = new FakeMigrationService(files: 50, progressEvents: 50);
            using var viewModel = new MigrationViewModel(service);
            var view = Realize(viewModel);

            await viewModel.StartAsync();
            Sta.PumpUntil(() => viewModel.Files.Count == 50, because: "50 files in the list");

            Assert.Equal(50, ((ListBox)view.FindName("FileList")!).Items.Count);
        });
    }

    [Fact]
    public void The_migration_is_not_slowed_down_by_a_busy_window()
    {
        Sta.RunAsync(async () =>
        {
            var service = new FakeMigrationService(files: 20, progressEvents: 200);
            using var viewModel = new MigrationViewModel(service);
            Realize(viewModel);

            var run = viewModel.StartAsync();

            // The window is busy: the dispatcher is not pumping for a while.
            Thread.Sleep(1_000);
            Assert.True(service.Finished, "The migration was still waiting for the UI thread.");

            Sta.PumpUntil(() => run.IsCompleted, because: "the migration to finish");
            await run;
        });
    }

    [Fact]
    public void Thousands_of_progress_events_cause_few_user_interface_updates()
    {
        Sta.RunAsync(async () =>
        {
            var service = new FakeMigrationService(files: 100, progressEvents: 5_000);
            using var viewModel = new MigrationViewModel(service);
            Realize(viewModel);
            var updates = 0;
            ((INotifyPropertyChanged)viewModel).PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(MigrationViewModel.Percent))
                {
                    updates++;
                }
            };

            await viewModel.StartAsync();
            Sta.PumpUntil(() => viewModel.Files.Count == 100, because: "all files in the list");

            Assert.InRange(updates, 1, 120);
        });
    }

    [Fact]
    public void The_final_state_is_correct()
    {
        Sta.RunAsync(async () =>
        {
            var service = new FakeMigrationService(files: 30, progressEvents: 300);
            using var viewModel = new MigrationViewModel(service);
            var view = Realize(viewModel);

            await viewModel.StartAsync();
            Sta.PumpUntil(() => viewModel.Files.Count == 30 && viewModel.Percent >= 100, because: "the migration to be shown as finished");

            Assert.Equal(100, viewModel.Percent);
            Assert.Equal("Completed", viewModel.Status);
            Assert.Equal(30, ((ListBox)view.FindName("FileList")!).Items.Count);
        });
    }

    private static MigrationView Realize(MigrationViewModel viewModel)
    {
        var view = new MigrationView { DataContext = viewModel };
        return Sta.Realize(view);
    }

    /// <summary>Raises events from a worker thread, exactly as the SDK does.</summary>
    private sealed class FakeMigrationService(int files, int progressEvents) : IMigrationService
    {
        public event EventHandler<FileUploadedEventArgs>? FileUploaded;

        public event EventHandler<MigrationProgressEventArgs>? ProgressChanged;

        public bool Finished { get; private set; }

        public Task RunAsync(CancellationToken cancellationToken = default) => Task.Run(() =>
        {
            for (var i = 1; i <= progressEvents; i++)
            {
                ProgressChanged?.Invoke(this, new MigrationProgressEventArgs(100.0 * i / progressEvents, i * 1024L));

                if (i % Math.Max(1, progressEvents / files) == 0 && FileUploaded is not null)
                {
                    var index = Math.Min(files, i / Math.Max(1, progressEvents / files));
                    FileUploaded.Invoke(this, new FileUploadedEventArgs($"documents/file-{index:D3}.pdf"));
                }
            }

            Finished = true;
        }, cancellationToken);
    }
}
