using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Git;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Tui.App;
using Gitunia.Tui.Rendering;
using Xunit;

namespace Gitunia.Tui.Tests.App;

public class EffectRunnerTests
{
    [Fact]
    public async Task GivenRestoredTabs_WhenStarting_ThenOnlyActiveTabLoadsUntilActivated()
    {
        using var first = TestRepository.Create();
        first.Commit("root", ("a.txt", "1"));
        using var second = TestRepository.Create();
        second.Commit("root", ("a.txt", "1"));
        var runner = new GitRunner();
        var scratch = Workspace.Empty;
        foreach (var path in new[] { first.Path, second.Path })
        {
            var location = await RepositoryDiscovery.DiscoverAsync(runner, path, CancellationToken.None);
            scratch = WorkspaceReducer.Reduce(scratch, new OpenRepositoryCommand(location)).Workspace;
        }

        var firstId = scratch.Tabs[0].Id;
        var secondId = scratch.Tabs[1].Id;
        var host = new WorkspaceHost(Workspace.Empty);
        var effects = new EffectRunner(runner, host, InlineUi.Post, GraphGlyphSet.Unicode, new StatusChannel());

        host.Restore(new WorkspaceUpdate(
            scratch with { ActiveTabId = firstId },
            [new LoadRepositoryEffect(firstId, scratch.Tabs[0].CommandPath), new LoadRepositoryEffect(secondId, scratch.Tabs[1].CommandPath)]));

        Assert.False(effects.SessionOf(firstId)!.IsPending);
        Assert.True(effects.SessionOf(secondId)!.IsPending);
        await WaitUntilAsync(() => !effects.SessionOf(firstId)!.IsLoading);
        Assert.Equal(0, effects.SessionOf(secondId)!.Graph.Count);

        host.Dispatch(new ActivateTabCommand(secondId));

        Assert.False(effects.SessionOf(secondId)!.IsPending);
        await WaitUntilAsync(() => !effects.SessionOf(secondId)!.IsLoading);
        Assert.Equal(1, effects.SessionOf(secondId)!.Graph.Count);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }

        Assert.True(condition());
    }
}
