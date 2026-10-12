using Gitunia.Core.Keymap;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Workspaces;
using Xunit;
using static Gitunia.Core.Tests.Sidebar.SidebarFixtures;

namespace Gitunia.Core.Tests.Sidebar;

public class SidebarReducerTests
{
    private static RepoState TabWithBranches() => Tab(refs: [Branch("a"), Branch("b"), Branch("c"), Remote("origin/a")]);

    private static RepoState Reduce(RepoState tab, SidebarCommand command) => RepoReducer.Reduce(tab, command);

    private static RepoState MoveTo(RepoState tab, int index) => Reduce(tab, new SetSidebarCursorCommand(index));

    private static string SelectedLabel(RepoState tab) => SidebarBuilder.Build(tab)[tab.Sidebar.Cursor].Label;

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
        Assert.Contains(SidebarBuilder.Build(tab), row => row.Label == "origin/a");
        Assert.Equal("Remotes", SelectedLabel(tab));

        tab = Reduce(tab, new ExpandSidebarCommand());
        Assert.Equal("origin/a", SelectedLabel(tab));
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
    public void GivenCursorOnLeaf_WhenExpanding_ThenFocusMovesToTheGraph()
    {
        var tab = MoveTo(TabWithBranches(), 1);
        tab = tab with { View = tab.View with { FocusedPane = PaneKind.Sidebar } };

        tab = Reduce(tab, new ExpandSidebarCommand());

        Assert.Equal(PaneKind.Graph, tab.View.FocusedPane);
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
}
