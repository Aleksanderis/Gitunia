using System.Collections.Immutable;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Status.Models;
using Xunit;
using static Gitunia.Core.Tests.Sidebar.SidebarFixtures;

namespace Gitunia.Core.Tests.Sidebar;

public class SidebarBuilderTests
{
    private static string[] Labels(IEnumerable<SidebarRow> rows) => [.. rows.Select(row => row.Label)];

    private static RepositoryState Unfolded(RepositoryState tab) => tab with { Sidebar = tab.Sidebar with { CollapsedSections = [] } };

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
            [
                "Branches", "Alpha", "zeta", "Remotes", "origin", "origin/main", "Tags", "v1", "Worktrees", "repo",
                "Stashes", "stash@{0}"
            ],
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

        Assert.Equal(["Branches", "feature", "feature/login", "Remotes", "origin/feature", "origin/feature/search"],
            Labels(rows));
        Assert.All(rows.Where(row => row.IsSection), row => Assert.True(row.IsExpanded));
    }

    [Fact]
    public void GivenPrefixedBranches_WhenBuilding_ThenFoldersAreClosedWithCountsAndFlatBranchesStayTopLevel()
    {
        var tab = Tab(
            refs: [Branch("main", isCurrent: true), Branch("feature/a"), Branch("feature/ui/b"), Branch("fix/c")]);

        var rows = SidebarBuilder.Build(tab);

        Assert.Equal(["Branches", "feature", "fix", "main"], Labels(rows).Take(4));
        var feature = rows.Single(row => row.Label == "feature");
        Assert.Equal(SidebarRowKind.Folder, feature.Kind);
        Assert.Equal(2, feature.Count);
        Assert.False(feature.IsExpanded);
        Assert.Equal(0, feature.Depth);
        Assert.Equal(4, rows[0].Count);
    }

    [Fact]
    public void GivenCurrentBranchInAFolder_WhenBuilding_ThenItsFoldersAreOpenAndOthersClosed()
    {
        var tab = Tab(
            refs:
            [
                Branch("feature/ui/x", isCurrent: true),
                Branch("feature/ui/y"),
                Branch("feature/z"),
                Branch("fix/c"),
            ]);

        var rows = SidebarBuilder.Build(tab);

        Assert.Equal(
            ["Branches", "feature", "ui", "feature/ui/x", "feature/ui/y", "feature/z", "fix"],
            Labels(rows).Take(7));
        Assert.Equal([-1, 0, 1, 2, 2, 1, 0], rows.Take(7).Select(row => row.Depth));
        Assert.Equal("x", rows[3].ShortName);
        Assert.Equal("feature/ui/x", rows[3].Label);
    }

    [Fact]
    public void GivenOpenedFolder_WhenBuilding_ThenItIsOpenAlthoughNotHoldingTheCurrentBranch()
    {
        var tab = Tab(refs: [Branch("main", isCurrent: true), Branch("feature/a"), Branch("fix/c")]);
        var key = SidebarFolders.KeyOf(SidebarSection.Branches, "feature");
        tab = tab with { Sidebar = tab.Sidebar with { OpenedFolders = [key] } };

        Assert.Equal(["Branches", "feature", "feature/a", "fix", "main"], Labels(SidebarBuilder.Build(tab)).Take(5));
    }

    [Fact]
    public void GivenChoicesMade_WhenTheCurrentBranchMoves_ThenTheChoicesStillHold()
    {
        var feature = SidebarFolders.KeyOf(SidebarSection.Branches, "feature");
        var fix = SidebarFolders.KeyOf(SidebarSection.Branches, "fix");
        var tab = Tab(refs: [Branch("main", isCurrent: true), Branch("feature/x"), Branch("fix/y")]);
        tab = tab with { Sidebar = tab.Sidebar with { OpenedFolders = [feature], ClosedFolders = [fix] } };

        var onFeature = tab with { Refs = [Branch("main"), Branch("feature/x", isCurrent: true), Branch("fix/y")] };
        var onFix = tab with { Refs = [Branch("main"), Branch("feature/x"), Branch("fix/y", isCurrent: true)] };

        string[] expected = ["Branches", "feature", "feature/x", "fix", "main"];
        Assert.Equal(expected, Labels(SidebarBuilder.Build(onFeature)).Take(5));
        Assert.Equal(expected, Labels(SidebarBuilder.Build(onFix)).Take(5));
    }

    [Fact]
    public void GivenRemoteBranches_WhenBuilding_ThenTheRemoteIsTheFirstFolderAndOpenAndPrefixFoldersClosed()
    {
        var tab = Unfolded(Tab(refs: [Remote("origin/main"), Remote("origin/feature/x"), Remote("up/main")]));

        var rows = SidebarBuilder.Build(tab)
            .SkipWhile(row => row.Label != "Remotes")
            .TakeWhile(row => row.Label != "Tags")
            .ToList();

        Assert.Equal(["Remotes", "origin", "feature", "origin/main", "up", "up/main"], Labels(rows));
        Assert.Equal([0, 1], rows.Where(row => row.IsFolder).Take(2).Select(row => row.Depth));
    }

    [Fact]
    public void GivenFilter_WhenBuildingPrefixedBranches_ThenFoldersWithAMatchAreUnfolded()
    {
        var tab = Tab(refs: [Branch("main", isCurrent: true), Branch("feature/ui/needle"), Branch("fix/other")]);
        tab = tab with { Sidebar = tab.Sidebar with { FilterText = "needle" } };

        var rows = SidebarBuilder.Build(tab);

        Assert.Equal(["Branches", "feature/ui", "feature/ui/needle"], Labels(rows));
        Assert.All(rows.Where(row => row.IsContainer), row => Assert.True(row.IsExpanded));
    }

    [Fact]
    public void GivenSingleChildFolderChain_WhenBuilding_ThenItIsOneRowAndTheLeafKeepsTheRestOfTheName()
    {
        var tab = Unfolded(Tab(refs: [Branch("main"), Branch("feature/ui/x"), Branch("feature/ui/y")]));
        var key = SidebarFolders.KeyOf(SidebarSection.Branches, "feature/ui");
        tab = tab with { Sidebar = tab.Sidebar with { OpenedFolders = [key] } };

        var rows = SidebarBuilder.Build(tab).Take(4).ToList();

        Assert.Equal(["Branches", "feature/ui", "feature/ui/x", "feature/ui/y"], Labels(rows));
        Assert.Equal(0, rows[1].Depth);
        Assert.Equal("x", rows[2].ShortName);
        Assert.Equal(1, rows[2].Depth);
    }

    [Fact]
    public void GivenFolderWithALeafAndAFolder_WhenBuilding_ThenTheChainIsNotCompressed()
    {
        var tab = Tab(refs: [Branch("main"), Branch("feature/a"), Branch("feature/ui/x")]);
        tab = tab with { Sidebar = tab.Sidebar with { FilterText = "e" } };

        Assert.Equal(["Branches", "feature", "feature/a", "ui", "feature/ui/x"], Labels(SidebarBuilder.Build(tab)));
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

    [Fact]
    public void GivenOverviewRead_WhenBuilding_ThenOtherWorktreesCarryTheirStatusAndTheirTrackingCounts()
    {
        var status = new WorktreeStatus(1, 2, 0, 3, 4);
        var tab = Tab(worktrees: [Worktree(RepositoryPath, "main", isMain: true), Worktree("/work/wt-a", "feature")]);
        var statuses = new Dictionary<string, WorktreeStatus> { ["/work/wt-a"] = status };
        tab = tab with { WorktreeStatuses = statuses.ToImmutableDictionary() };

        var rows = SidebarBuilder.Build(Unfolded(tab));

        var other = rows.Single(row => row is { Kind: SidebarRowKind.Worktree, Label: "wt-a" });
        Assert.Equal(status, other.Changes);
        Assert.Equal((3, 4), (other.Ahead, other.Behind));
        Assert.Null(rows.Single(row => row is { Kind: SidebarRowKind.Worktree, Label: "repo" }).Changes);
    }

    [Fact]
    public void GivenTabWorkingTreeLoaded_WhenBuilding_ThenItsOwnWorktreeIsCountedFromThatStatus()
    {
        var head = new HeadState(HeadKind.Branch, "main", null);
        var entry = new StatusEntry(
            StatusEntryKind.Untracked,
            "new.txt",
            null,
            FileChangeKind.Unmodified,
            FileChangeKind.Unmodified,
            null,
            ConflictKind.None,
            null);
        var snapshot = new StatusSnapshot(new StatusBranch(head, null, null, null), [entry], []);
        var tab = Tab(worktrees: [Worktree(RepositoryPath, "main", isMain: true)]);
        tab = tab with { WorkingTree = WorkingTreeState.Reconcile(null, snapshot) };

        var row = SidebarBuilder.Build(Unfolded(tab)).Single(candidate => candidate.Kind == SidebarRowKind.Worktree);

        Assert.Equal(new WorktreeStatus(0, 1, 0, 0, 0), row.Changes);
    }
}
