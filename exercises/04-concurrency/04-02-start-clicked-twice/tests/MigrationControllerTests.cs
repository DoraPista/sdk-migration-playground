using Gym.TestUtilities;
using MigrationKit.Control;

namespace Ex0402.Control.Tests;

public sealed class MigrationControllerTests
{
    [Fact]
    public async Task Starting_again_while_running_returns_the_running_migration()
    {
        var platform = new FakePlatform();
        var executor = new FakeExecutor();
        var controller = new MigrationController(platform, executor);

        var first = await controller.StartAsync("CUST-1001");
        var second = await controller.StartAsync("CUST-1001");

        Assert.Same(first, second);
        Assert.Equal(1, platform.Created);
    }

    [Fact]
    public async Task Double_click_while_the_migration_is_being_created_creates_only_one_migration()
    {
        var creationGate = new AsyncGate();
        var platform = new FakePlatform(creationGate);
        var controller = new MigrationController(platform, new FakeExecutor());

        var click1 = controller.StartAsync("CUST-1001");
        var click2 = controller.StartAsync("CUST-1001");
        await creationGate.WhenWaitingAsync(1);
        await Task.Delay(50);
        creationGate.Open();

        var handles = await Task.WhenAll(click1, click2).WithTimeout();

        Assert.Equal(1, platform.Created);
        Assert.Equal(handles[0].MigrationId, handles[1].MigrationId);
    }

    [Fact]
    public async Task A_new_migration_can_be_started_after_the_previous_one_finished()
    {
        var platform = new FakePlatform();
        var executor = new FakeExecutor();
        var controller = new MigrationController(platform, executor);

        var first = await controller.StartAsync("CUST-1001");
        executor.FinishAll();
        await first.Completion.WithTimeout();
        var second = await controller.StartAsync("CUST-1001");

        Assert.NotEqual(first.MigrationId, second.MigrationId);
        Assert.Equal(2, platform.Created);
    }

    [Fact]
    public async Task If_starting_fails_the_user_can_try_again()
    {
        var platform = new FakePlatform { FailNextCreate = true };
        var controller = new MigrationController(platform, new FakeExecutor());

        await Assert.ThrowsAsync<HttpRequestException>(() => controller.StartAsync("CUST-1001"));
        var retry = await controller.StartAsync("CUST-1001").WithTimeout();

        Assert.Equal("mig-2", retry.MigrationId);
    }

    private sealed class FakePlatform(AsyncGate? gate = null) : IMigrationPlatform
    {
        private int _created;
        private int _calls;

        public bool FailNextCreate { get; set; }

        public int Created => Volatile.Read(ref _created);

        public async Task<string> CreateMigrationAsync(string customerId, CancellationToken cancellationToken)
        {
            var call = Interlocked.Increment(ref _calls);
            if (gate is not null)
            {
                await gate.WaitAsync(cancellationToken);
            }

            await Task.Yield();
            if (FailNextCreate)
            {
                FailNextCreate = false;
                throw new HttpRequestException("The platform could not be reached.");
            }

            Interlocked.Increment(ref _created);
            return $"mig-{call}";
        }
    }

    private sealed class FakeExecutor : IMigrationExecutor
    {
        private readonly TaskCompletionSource _finish = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void FinishAll() => _finish.TrySetResult();

        public Task RunAsync(string migrationId, CancellationToken cancellationToken) => _finish.Task.WaitAsync(cancellationToken);
    }
}
