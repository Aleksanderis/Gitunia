using Gitunia.Core.Keymap;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Xunit;
using static Gitunia.Core.Tests.Sidebar.SidebarFixtures;

namespace Gitunia.Core.Tests.Sidebar;

[Trait("Os", "Independent")]
public class SidebarReducerTests
{
    private static RepositoryState TabWithBranches() => Tab(refs: [Branch("a"), Branch("b"), Branch("c"), Remote("origin/a")]);

    private static RepositoryState Reduce(RepositoryState tab, SidebarCommand command) => RepositoryReducer.Reduce(tab, command);

    private static RepositoryState MoveTo(RepositoryState tab, int index) => Reduce(tab, new SetSidebarCursorCommand(index));

    private static string SelectedLabel(RepositoryState tab) => SidebarBuilder.Build(tab)[tab.Sidebar.Cursor].Label;

    private static RepositoryState TabWithFolders() =>
        Tab(refs: [Branch("main", isCurrent: true), Branch("feature/ui/a"), Branch("feature/b")]);

    private static int IndexOf(RepositoryState tab, string label) =>
        SidebarBuilder.Build(tab).ToList().FindIndex(row => row.Label == label);

    [Fact]
    public void GivenCursorOnTop_WhenMovingDownThreeTimes_ThenCursorWalksTheRowsAndStopsAtTheEnd()
    {
        var tab = TabWithBranches();

        tab = Reduce(tab, new MoveSidebarCursorCommand(ActionIds.ListDown, 3, 10));
        Assert.Equal("c", SelectedLabel(tab));

        tab = Reduce(tab, new MoveSidebarCursorCommand(ActionIds.ListBottom, null, 10));
        Assert.Equal("Stashes", SelectedLabel(tab));

        tab = Reduce(tab, new MoveSidebarCursorCommand(ActionIds.ListDown, null, 10));
        Assert.Equal("Stashes", SelectedLabel(tab));
    }

    [Fact]
    public void GivenNonMotionAction_WhenMoving_ThenNothingChanges()
    {
        var tab = MoveTo(TabWithBranches(), 2);

        Assert.Equal(tab, Reduce(tab, new MoveSidebarCursorCommand(ActionIds.GraphJumpToHead, null, 10)));
    }

    [Fact]
    public void GivenCollapsedSectionHeader_WhenExpanding_ThenEntriesAppearAndAnotherExpandStepsIn()
    {
        var tab = TabWithBranches();
        var remotes = SidebarBuilder.Build(tab).ToList().FindIndex(row => row.Label == "Remotes");
        tab = MoveTo(tab, remotes);

        tab = Reduce(tab, new ExpandSidebarCommand());
        Assert.Contains(SidebarBuilder.Build(tab), row => row.Label == "origin");
        Assert.Equal("Remotes", SelectedLabel(tab));

        tab = Reduce(tab, new ExpandSidebarCommand());
        Assert.Equal("origin", SelectedLabel(tab));
    }

    [Fact]
    public void GivenClosedFolder_WhenExpandingAndCollapsing_ThenItOpensStepsInAndClosesAgain()
    {
        var tab = MoveTo(TabWithFolders(), 1);
        Assert.Equal("feature", SelectedLabel(tab));
        Assert.DoesNotContain(SidebarBuilder.Build(tab), row => row.Label == "feature/b");

        tab = Reduce(tab, new ExpandSidebarCommand());
        Assert.Contains(SidebarBuilder.Build(tab), row => row.Label == "feature/b");
        Assert.Equal("feature", SelectedLabel(tab));

        tab = Reduce(tab, new ExpandSidebarCommand());
        Assert.Equal("feature/b", SelectedLabel(tab));

        tab = MoveTo(tab, 1);
        tab = Reduce(tab, new CollapseSidebarCommand());
        Assert.DoesNotContain(SidebarBuilder.Build(tab), row => row.Label == "feature/b");
        Assert.Single(tab.Sidebar.ClosedFolders);
    }

    [Fact]
    public void GivenOpenCurrentFolder_WhenCollapsing_ThenItClosesAndTheToggleIsRemembered()
    {
        var tab = Tab(refs: [Branch("feature/a", isCurrent: true), Branch("feature/b")]);
        tab = MoveTo(tab, 1);

        tab = Reduce(tab, new CollapseSidebarCommand());

        Assert.Equal(["Branches", "feature"], SidebarBuilder.Build(tab).Take(2).Select(row => row.Label));
        Assert.Single(tab.Sidebar.ClosedFolders);
    }

    [Fact]
    public void GivenBranchInNestedFolder_WhenCollapsing_ThenCursorGoesToItsFolderThenTheParentFolder()
    {
        var tab = Tab(refs: [Branch("feature/ui/a", isCurrent: true), Branch("feature/b")]);
        tab = MoveTo(tab, IndexOf(tab, "feature/ui/a"));

        tab = Reduce(tab, new CollapseSidebarCommand());
        Assert.Equal("ui", SelectedLabel(tab));

        tab = MoveTo(tab, IndexOf(tab, "ui"));
        tab = Reduce(tab, new ExpandSidebarCommand());
        tab = Reduce(tab, new ExpandSidebarCommand());
        Assert.Equal("feature/ui/a", SelectedLabel(tab));
    }

    [Fact]
    public void GivenFolderRow_WhenTogglingAndFiltering_ThenToggleFlipsItAndFilterIgnoresIt()
    {
        var tab = MoveTo(TabWithFolders(), 1);

        tab = Reduce(tab, new ToggleSidebarSectionCommand());
        Assert.Contains(SidebarBuilder.Build(tab), row => row.Label == "feature/b");

        tab = Reduce(tab, new SetSidebarFilterCommand("ui/a"));
        var labels = SidebarBuilder.Build(tab).Select(row => row.Label).ToList();
        Assert.Equal(["Branches", "feature/ui", "feature/ui/a"], labels);
        Assert.Equal("feature/ui", SelectedLabel(tab));
    }

    [Fact]
    public void GivenExpandedSection_WhenCollapsing_ThenEntriesHideAndCursorStaysOnTheHeader()
    {
        var tab = Reduce(TabWithBranches(), new CollapseSidebarCommand());

        Assert.DoesNotContain(SidebarBuilder.Build(tab), row => row.Label == "a");
        Assert.Equal("Branches", SelectedLabel(tab));
    }

    [Fact]
    public void GivenCursorOnEntry_WhenCollapsing_ThenCursorMovesToItsSectionHeader()
    {
        var tab = MoveTo(TabWithBranches(), 3);

        tab = Reduce(tab, new CollapseSidebarCommand());

        Assert.Equal("Branches", SelectedLabel(tab));
        Assert.Contains(SidebarBuilder.Build(tab), row => row.Label == "c");
    }

    [Fact]
    public void GivenCursorOnLeaf_WhenExpanding_ThenFocusStaysInTheSidebar()
    {
        var tab = MoveTo(TabWithBranches(), 1);
        tab = tab with { View = tab.View with { FocusedPane = PaneKind.Sidebar } };

        tab = Reduce(tab, new ExpandSidebarCommand());

        Assert.Equal(PaneKind.Sidebar, tab.View.FocusedPane);
    }

    [Fact]
    public void GivenSectionHeader_WhenToggling_ThenItFoldsAndUnfolds()
    {
        var tab = TabWithBranches();

        tab = Reduce(tab, new ToggleSidebarSectionCommand());
        Assert.DoesNotContain(SidebarBuilder.Build(tab), row => row.Label == "a");

        tab = Reduce(tab, new ToggleSidebarSectionCommand());
        Assert.Contains(SidebarBuilder.Build(tab), row => row.Label == "a");
    }

    [Fact]
    public void GivenFilter_WhenSetting_ThenCursorLandsOnTheFirstMatchAndClearingRestoresTheList()
    {
        var tab = Reduce(TabWithBranches(), new SetSidebarFilterCommand("b"));
        Assert.Equal("b", SelectedLabel(tab));

        tab = Reduce(tab, new SetSidebarFilterCommand(string.Empty));
        Assert.Equal("Branches", SelectedLabel(tab));
        Assert.Equal(3, SidebarBuilder.Build(tab).Count(row => !row.IsSection));
    }

    [Fact]
    public void GivenFilterWithoutMatches_WhenSetting_ThenRowsAreEmptyAndMotionsDoNotThrow()
    {
        var tab = Reduce(TabWithBranches(), new SetSidebarFilterCommand("zzz"));

        tab = Reduce(tab, new MoveSidebarCursorCommand(ActionIds.ListDown, null, 10));
        tab = Reduce(tab, new ExpandSidebarCommand());
        tab = Reduce(tab, new CollapseSidebarCommand());

        Assert.Empty(SidebarBuilder.Build(tab));
    }

    [Fact]
    public void GivenCursorPastTheEnd_WhenReducing_ThenItIsClamped()
    {
        var tab = MoveTo(TabWithBranches(), 999);

        Assert.Equal(SidebarBuilder.Build(tab).Length - 1, tab.Sidebar.Cursor);
    }

    [Fact]
    public void GivenFolderOpenedByHand_WhenAnotherBranchBecomesCurrent_ThenItStaysOpen()
    {
        var tab = Tab(refs: [Branch("main", isCurrent: true), Branch("feature/x")]);
        tab = MoveTo(tab, IndexOf(tab, "feature"));
        tab = Reduce(tab, new ExpandSidebarCommand());

        tab = tab with { Refs = [Branch("main"), Branch("feature/x", isCurrent: true)] };

        Assert.Contains(SidebarBuilder.Build(tab), row => row.Label == "feature/x");
        tab = Reduce(tab, new CollapseSidebarCommand());
        tab = tab with { Refs = [Branch("main", isCurrent: true), Branch("feature/x")] };
        Assert.DoesNotContain(SidebarBuilder.Build(tab), row => row.Label == "feature/x");
    }

    [Fact]
    public void GivenFilterAndOpenFolder_WhenCollapsing_ThenCursorMovesToTheParentInsteadOfDoingNothing()
    {
        var tab = Reduce(TabWithFolders(), new SetSidebarFilterCommand("ui/a"));
        tab = MoveTo(tab, IndexOf(tab, "feature/ui"));

        tab = Reduce(tab, new CollapseSidebarCommand());

        Assert.Equal("Branches", SelectedLabel(tab));
    }
}
