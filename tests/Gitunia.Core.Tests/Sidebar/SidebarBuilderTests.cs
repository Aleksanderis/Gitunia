using Gitunia.Core.Sidebar;
using Gitunia.Core.Workspaces;
using Xunit;
using static Gitunia.Core.Tests.Sidebar.SidebarFixtures;

namespace Gitunia.Core.Tests.Sidebar;

public class SidebarBuilderTests
{
    private static string[] Labels(IEnumerable<SidebarRow> rows) => [.. rows.Select(row => row.Label)];

    private static RepoState Unfolded(RepoState tab) => tab with { Sidebar = tab.Sidebar with { CollapsedSections = [] } };

    [Fact]
    public void GivenNothingLoaded_WhenBuilding_ThenOnlySectionHeadersWithZeroCounts()
    {
        var rows = SidebarBuilder.Build(Tab());

        Assert.Equal(["Branches", "Remotes", "Tags", "Worktrees", "Stashes"], Labels(rows));
        Assert.All(rows, row => Assert.Equal(0, row.Count));
    }

    [Fact]
    public void GivenAllKindsOfEntries_WhenBuilding_ThenSectionsListTheirEntriesInOrder()
    {
        var tab = Unfolded(Tab(
            refs: [Branch("zeta"), Branch("Alpha"), Remote("origin/main"), Remote("origin/HEAD", "refs/remotes/origin/main"), Tag("v1")],
            worktrees: [Worktree("/work/repo", "main", isMain: true)],
            stashes: [Stash(0, "On main: wip")]));

        var rows = SidebarBuilder.Build(tab);

        Assert.Equal(
            ["Branches", "Alpha", "zeta", "Remotes", "origin/main", "Tags", "v1", "Worktrees", "repo", "Stashes", "stash@{0}"],
            Labels(rows));
        Assert.Equal(2, rows[0].Count);
    }

    [Fact]
    public void GivenDefaultState_WhenBuilding_ThenRemotesAndTagsAreFoldedButCounted()
    {
        var tab = Tab(refs: [Branch("main"), Remote("origin/main"), Tag("v1")]);

        var rows = SidebarBuilder.Build(tab);

        Assert.Equal(["Branches", "main", "Remotes", "Tags", "Worktrees", "Stashes"], Labels(rows));
        var remotes = rows.Single(row => row.Label == "Remotes");
        Assert.False(remotes.IsExpanded);
        Assert.Equal(1, remotes.Count);
    }

    [Fact]
    public void GivenBranchesInWorktrees_WhenBuilding_ThenEachBranchNamesTheWorktreeHoldingIt()
    {
        var tab = Tab(
            refs: [Branch("main", "/work/repo", isCurrent: true), Branch("feature", "/work/wt feature/"), Branch("free")],
            worktrees:
            [
                Worktree("/work/repo", "main", isMain: true),
                Worktree("/work/wt feature", "feature"),
                Worktree("/work/detached", null, isLocked: true, isPrunable: true),
            ]);

        var rows = SidebarBuilder.Build(tab);

        Assert.Equal("repo", rows.Single(row => row.Label == "main").Annotation);
        Assert.True(rows.Single(row => row.Label == "main").IsCurrent);
        Assert.Equal("wt feature", rows.Single(row => row.Label == "feature").Annotation);
        Assert.Equal(string.Empty, rows.Single(row => row.Label == "free").Annotation);
        Assert.Equal("main", rows.Single(row => row is { Kind: SidebarRowKind.Worktree, Label: "repo" }).Annotation);
        Assert.True(rows.Single(row => row is { Kind: SidebarRowKind.Worktree, Label: "repo" }).IsCurrent);
        Assert.Equal("detached locked prunable", rows.Single(row => row is { Kind: SidebarRowKind.Worktree, Label: "detached" }).Annotation);
    }

    [Fact]
    public void GivenFilter_WhenBuilding_ThenOnlyMatchesRemainAndFoldedSectionsOpen()
    {
        var tab = Tab(refs: [Branch("main"), Branch("feature/login"), Remote("origin/feature/search"), Tag("v1")]);
        tab = tab with { Sidebar = tab.Sidebar with { FilterText = "FEATURE" } };

        var rows = SidebarBuilder.Build(tab);

        Assert.Equal(["Branches", "feature/login", "Remotes", "origin/feature/search"], Labels(rows));
        Assert.All(rows.Where(row => row.IsSection), row => Assert.True(row.IsExpanded));
    }

    [Fact]
    public void GivenFilterMatchingAWorktreeName_WhenBuilding_ThenBranchesInThatWorktreeMatch()
    {
        var tab = Tab(refs: [Branch("feature", "/work/wt-a"), Branch("main", "/work/repo")]);
        tab = tab with { Sidebar = tab.Sidebar with { FilterText = "wt-a" } };

        Assert.Equal(["Branches", "feature"], Labels(SidebarBuilder.Build(tab)));
    }

    [Fact]
    public void GivenUpstreamCounts_WhenBuilding_ThenAheadAndBehindAreCarried()
    {
        var tab = Tab(refs: [Branch("main", ahead: 2, behind: 1)]);

        var row = SidebarBuilder.Build(tab).Single(entry => entry.Label == "main");

        Assert.Equal((2, 1), (row.Ahead, row.Behind));
    }
}
