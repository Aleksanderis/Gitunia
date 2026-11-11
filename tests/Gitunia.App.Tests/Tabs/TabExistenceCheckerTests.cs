using Gitunia.App.Picker;
using Gitunia.App.Tabs;
using Gitunia.App.Tests.Tags;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Repositories.Models;
using Xunit;

namespace Gitunia.App.Tests.Tabs;

public sealed class TabExistenceCheckerTests : IDisposable
{
    private static readonly TimeSpan ShortTimeout = TimeSpan.FromMilliseconds(100);

    // Generous, so a healthy path answering late on a loaded runner is never mistaken for a hung one.
    private static readonly TimeSpan HealthyAnswerBudget = TimeSpan.FromSeconds(3);
    private readonly ManualResetEventSlim _release = new();

    [Fact]
    public async Task GivenCheckThatBlocks_WhenStarting_ThenStartReturnsAtOnceAndTheTabIsMarkedMissingAfterTheTimeout()
    {
        var dispatcher = new RecordingDispatcher();
        var checker = new TabExistenceChecker(dispatcher, new BlockingDirectories(_release, []), ShortTimeout);
        var tab = Tab(1, "hung");

        var running = checker.Start([tab], TestContext.Current.CancellationToken);

        Assert.False(running.IsCompleted);
        var command = Assert.IsType<TabCommand>(await dispatcher.NextAsync(TestContext.Current.CancellationToken));
        Assert.Equal(tab.Id, command.TabId);
        Assert.Equal(new SetMissingCommand(true, tab.CommandPath), command.Command);
        await running;
    }

    [Fact]
    public async Task GivenOneHungAndOneExistingTab_WhenChecking_ThenOnlyTheHungOneIsMarkedMissing()
    {
        var dispatcher = new RecordingDispatcher();
        var good = Tab(1, "good");
        var hung = Tab(2, "hung");
        var checker = new TabExistenceChecker(dispatcher, new BlockingDirectories(_release, [good.CommandPath]), HealthyAnswerBudget);

        await checker.Start([good, hung], TestContext.Current.CancellationToken);

        var command = Assert.IsType<TabCommand>(await dispatcher.NextAsync(TestContext.Current.CancellationToken));
        Assert.Equal(hung.Id, command.TabId);
        using var guard = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        guard.CancelAfter(ShortTimeout);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => dispatcher.NextAsync(guard.Token));
    }

    [Fact]
    public async Task GivenGoneDirectory_WhenChecking_ThenTheTabIsMarkedMissing()
    {
        var dispatcher = new RecordingDispatcher();
        var tab = Tab(3, "gone");
        var checker = new TabExistenceChecker(dispatcher, new GoneDirectories(), TimeSpan.FromSeconds(30));

        await checker.Start([tab], TestContext.Current.CancellationToken);

        var command = Assert.IsType<TabCommand>(await dispatcher.NextAsync(TestContext.Current.CancellationToken));
        Assert.Equal(tab.Id, command.TabId);
    }

    public void Dispose()
    {
        _release.Set();
        _release.Dispose();
    }

    private static RepositoryState Tab(int number, string name)
    {
        var root = Path.Combine(Path.GetTempPath(), "gitunia-existence", name);
        var location = new RepositoryLocation(root, Path.Combine(root, ".git"), Path.Combine(root, ".git"), false, false);

        return RepositoryState.Create(new TabId(number), location);
    }

    private sealed class GoneDirectories : IDirectoryLister
    {
        public IReadOnlyList<string> ListDirectories(string directory) => [];

        public bool IsRepository(string directory) => false;

        public bool DirectoryExists(string directory) => false;
    }

    private sealed class BlockingDirectories(ManualResetEventSlim release, string[] existing) : IDirectoryLister
    {
        public IReadOnlyList<string> ListDirectories(string directory) => [];

        public bool IsRepository(string directory) => false;

        public bool DirectoryExists(string directory)
        {
            if (existing.Contains(directory))
            {
                return true;
            }

            release.Wait();

            return true;
        }
    }
}
