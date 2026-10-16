using System.Collections.Immutable;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Status.Models;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Tui.Tests.App.Sidebar;
using Xunit;

namespace Gitunia.Tui.Tests.Views.Lists;

public class SidebarListSourceTests
{
    [Fact]
    public async Task GivenLoadedSidebar_WhenOverviewArrivesAndOwnStatusChanges_ThenWorktreeRowsAreRebuilt()
    {
        using var repository = TestRepositories.WithWorktree(out var path);
        using var harness = await SidebarHarness.LoadAsync(repository.WorkingTreePath);
        var other = harness.Source.Rows.Single(row => row.Path == path);
        Assert.Null(other.Changes);

        var overview = ImmutableDictionary<string, WorktreeStatus>.Empty.Add(path, new WorktreeStatus(0, 2, 0, 1, 0));
        harness.Send(new WorktreeStatusesLoadedCommand(overview));

        var updated = harness.Source.Rows.Single(row => row.Path == path);
        Assert.Equal(new WorktreeStatus(0, 2, 0, 1, 0), updated.Changes);

        var file = Path.Combine(repository.WorkingTreePath, "new.txt");
        await File.WriteAllTextAsync(file, "x", TestContext.Current.CancellationToken);
        var snapshot = await TestGit.Services.Status.ReadStatusAsync(
            harness.Tab.Location,
            false,
            UntrackedFilesMode.All,
            TestContext.Current.CancellationToken);
        harness.Send(new WorkingTreeLoadedCommand(snapshot));

        var own = harness.Source.Rows.Single(row => row is { Kind: SidebarRowKind.Worktree, IsCurrent: true });
        Assert.Equal(1, own.Changes!.Changed);
    }
}
