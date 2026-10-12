using Gitunia.Core.Keymap;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Workspaces;
using Gitunia.Git;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.Tui.Tests.App;

public class SidebarActionHandlerTests
{
    private static TestRepository BranchyRepository()
    {
        var repository = TestRepository.Create();
        repository.Commit("one", ("a.txt", "1")).Branch("old").Commit("two", ("a.txt", "2")).Tag("v2").Branch("feature");
        return repository;
    }

    [Fact]
    public async Task GivenABranchRow_WhenCopying_ThenItsNameGoesToTheClipboard()
    {
        using var repository = BranchyRepository();
        using var harness = await SidebarHarness.LoadAsync(repository.Path);
        harness.FocusRow("old");

        Assert.True(harness.Handler.TryHandle(ActionIds.SidebarCopy, null));

        Assert.Equal("\u001b]52;c;b2xk\u001b\\", harness.Clipboard.ToString());
    }

    [Fact]
    public async Task GivenASectionRow_WhenCopying_ThenNothingIsCopied()
    {
        using var repository = BranchyRepository();
        using var harness = await SidebarHarness.LoadAsync(repository.Path);
        harness.FocusRow("Tags");

        harness.Handler.TryHandle(ActionIds.SidebarCopy, null);

        Assert.Equal(string.Empty, harness.Clipboard.ToString());
        Assert.Equal("Select an entry to copy", harness.Handler.Message);
    }

    [Fact]
    public async Task GivenGraphFocus_WhenTabPressedTwice_ThenFocusGoesToSidebarAndBack()
    {
        using var repository = BranchyRepository();
        using var harness = await SidebarHarness.LoadAsync(repository.Path);

        Assert.True(harness.Handler.TryHandle(ActionIds.PaneNext, null));
        Assert.Equal(PaneKind.Sidebar, harness.Tab.View.FocusedPane);
        Assert.True(harness.Handler.TryHandle(ActionIds.PaneNext, null));
        Assert.Equal(PaneKind.Graph, harness.Tab.View.FocusedPane);
    }

    [Fact]
    public async Task GivenAnOpenDiff_WhenTabbing_ThenTheDiffJoinsTheCycleAfterTheDetailPane()
    {
        using var repository = BranchyRepository();
        using var harness = await SidebarHarness.LoadAsync(repository.Path);
        harness.DetailVisible = true;
        harness.Handler.IsDiffVisible = () => true;
        harness.Send(new FocusPaneCommand(PaneKind.Detail));

        harness.Handler.TryHandle(ActionIds.PaneNext, null);
        Assert.Equal(PaneKind.Diff, harness.Tab.View.FocusedPane);
        harness.Handler.TryHandle(ActionIds.PaneFocusLeft, null);
        Assert.Equal(PaneKind.Detail, harness.Tab.View.FocusedPane);
    }

    [Fact]
    public async Task GivenDetailAndDiffBelow_WhenMovingDownAndUp_ThenFocusGoesToTheDetailPaneAndBackToTheGraph()
    {
        using var repository = BranchyRepository();
        using var harness = await SidebarHarness.LoadAsync(repository.Path);
        harness.DetailVisible = true;
        harness.Handler.IsDiffVisible = () => true;
        harness.Send(new FocusPaneCommand(PaneKind.Graph));

        Assert.True(harness.Handler.TryHandle(ActionIds.PaneFocusDown, null));
        Assert.Equal(PaneKind.Detail, harness.Tab.View.FocusedPane);
        Assert.True(harness.Handler.TryHandle(ActionIds.PaneFocusUp, null));
        Assert.Equal(PaneKind.Graph, harness.Tab.View.FocusedPane);
    }

    [Fact]
    public async Task GivenOnlyTheDiffBelow_WhenMovingDown_ThenTheDiffIsFocused()
    {
        using var repository = BranchyRepository();
        using var harness = await SidebarHarness.LoadAsync(repository.Path);
        harness.DetailVisible = false;
        harness.Handler.IsDiffVisible = () => true;
        harness.Send(new FocusPaneCommand(PaneKind.Graph));

        harness.Handler.TryHandle(ActionIds.PaneFocusDown, null);

        Assert.Equal(PaneKind.Diff, harness.Tab.View.FocusedPane);
    }

    [Fact]
    public async Task GivenAZoomedPane_WhenTabbing_ThenHiddenPanesAreSkipped()
    {
        using var repository = BranchyRepository();
        using var harness = await SidebarHarness.LoadAsync(repository.Path);
        harness.SidebarVisible = false;
        harness.DetailVisible = false;
        harness.Handler.IsGraphVisible = () => false;
        harness.Handler.IsDiffVisible = () => true;
        harness.Send(new FocusPaneCommand(PaneKind.Diff));

        harness.Handler.TryHandle(ActionIds.PaneNext, null);

        Assert.Equal(PaneKind.Diff, harness.Tab.View.FocusedPane);
    }

    [Fact]
    public async Task GivenHiddenSidebar_WhenCyclingPanes_ThenFocusStaysOnTheGraph()
    {
        using var repository = BranchyRepository();
        using var harness = await SidebarHarness.LoadAsync(repository.Path);
        harness.SidebarVisible = false;

        harness.Handler.TryHandle(ActionIds.PaneNext, null);
        harness.Handler.TryHandle(ActionIds.PaneFocusLeft, null);

        Assert.Equal(PaneKind.Graph, harness.Tab.View.FocusedPane);
    }

    [Fact]
    public async Task GivenGraphFocus_WhenListMotionArrives_ThenSidebarDoesNotHandleIt()
    {
        using var repository = BranchyRepository();
        using var harness = await SidebarHarness.LoadAsync(repository.Path);

        Assert.False(harness.Handler.TryHandle(ActionIds.ListDown, null));
        Assert.Equal(0, harness.Tab.Sidebar.Cursor);
    }

    [Fact]
    public async Task GivenSidebarFocus_WhenMovingAndActivatingABranch_ThenItsCommitIsSelectedInTheGraphAndFocusStays()
    {
        using var repository = BranchyRepository();
        using var harness = await SidebarHarness.LoadAsync(repository.Path);
        harness.FocusRow("old");

        Assert.True(harness.Handler.TryHandle(ActionIds.SidebarActivate, null));

        // Newest first: "two" is item 0, "one" (where `old` points) is item 1.
        Assert.Equal(1, harness.SelectedGraphItem);
        Assert.Equal(PaneKind.Sidebar, harness.Tab.View.FocusedPane);
        Assert.Equal(string.Empty, harness.Handler.Message);
    }

    [Fact]
    public async Task GivenFoldedTags_WhenExpandingAndActivatingATag_ThenItsCommitIsSelected()
    {
        using var repository = BranchyRepository();
        using var harness = await SidebarHarness.LoadAsync(repository.Path);
        harness.FocusRow("Tags");

        harness.Handler.TryHandle(ActionIds.SidebarExpand, null);
        harness.Handler.TryHandle(ActionIds.SidebarExpand, null);
        harness.Handler.TryHandle(ActionIds.SidebarActivate, null);

        Assert.Equal("v2", harness.Source.Rows[harness.Tab.Sidebar.Cursor].Label);
        Assert.Equal(0, harness.SelectedGraphItem);
    }

    [Fact]
    public async Task GivenCursorOnLeaf_WhenPressingL_ThenFocusMovesToTheGraph()
    {
        using var repository = BranchyRepository();
        using var harness = await SidebarHarness.LoadAsync(repository.Path);
        harness.FocusRow("feature");

        harness.Handler.TryHandle(ActionIds.SidebarExpand, null);

        Assert.Equal(PaneKind.Graph, harness.Tab.View.FocusedPane);
    }

    [Fact]
    public async Task GivenStashRow_WhenActivating_ThenAMessageExplainsItIsNotInTheGraph()
    {
        using var repository = BranchyRepository();
        repository.WriteFile("a.txt", "dirty");
        repository.Git("stash", "push", "-q", "-m", "wip");
        using var harness = await SidebarHarness.LoadAsync(repository.Path);
        harness.FocusRow("stash@{0}");

        harness.Handler.TryHandle(ActionIds.SidebarActivate, null);

        Assert.Null(harness.SelectedGraphItem);
        Assert.Contains("Stash", harness.Handler.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenBranchNotInTheLoadedGraph_WhenActivating_ThenAMessageSaysSo()
    {
        using var repository = BranchyRepository();
        using var harness = await SidebarHarness.LoadAsync(repository.Path);
        harness.FocusRow("feature");
        harness.HideGraph = true;

        harness.Handler.TryHandle(ActionIds.SidebarActivate, null);

        Assert.Null(harness.SelectedGraphItem);
        Assert.Equal("feature is not in the graph", harness.Handler.Message);
    }

    [Fact]
    public async Task GivenSidebarFocus_WhenTypingAFilter_ThenInsertModeIsEnteredAndRowsNarrowDown()
    {
        using var repository = BranchyRepository();
        using var harness = await SidebarHarness.LoadAsync(repository.Path);
        harness.FocusRow("Branches");

        harness.Handler.TryHandle(ActionIds.SidebarFilter, null);
        harness.Handler.OnKey(new KeyEvent("o"));
        harness.Handler.OnKey(new KeyEvent("l"));

        Assert.Equal(KeyMode.Insert, harness.Mode);
        Assert.True(harness.Handler.IsEditingFilter);
        Assert.Equal("ol", harness.Tab.Sidebar.FilterText);
        Assert.Equal(["Branches", "old"], harness.Source.Rows.Select(row => row.Label));

        harness.Handler.OnKey(new KeyEvent(KeyNames.Backspace));
        Assert.Equal("o", harness.Tab.Sidebar.FilterText);

        harness.Handler.TryHandle(ActionIds.InputAccept, null);
        Assert.Equal(KeyMode.Normal, harness.Mode);
        Assert.Equal("o", harness.Tab.Sidebar.FilterText);
    }

    [Fact]
    public async Task GivenFilterBeingTyped_WhenEscapePressed_ThenFilterIsClearedAndNormalModeReturns()
    {
        using var repository = BranchyRepository();
        using var harness = await SidebarHarness.LoadAsync(repository.Path);
        harness.FocusRow("Branches");
        harness.Handler.TryHandle(ActionIds.SidebarFilter, null);
        foreach (var letter in "zzz") { harness.Handler.OnKey(new KeyEvent(letter.ToString())); }

        harness.Handler.TryHandle(ActionIds.Cancel, null);

        Assert.Equal(KeyMode.Normal, harness.Mode);
        Assert.Equal(string.Empty, harness.Tab.Sidebar.FilterText);
        Assert.False(harness.Handler.IsEditingFilter);
    }

    [Fact]
    public async Task GivenAcceptedFilter_WhenEscapePressedTwice_ThenFilterClearsFirstThenFocusReturnsToGraph()
    {
        using var repository = BranchyRepository();
        using var harness = await SidebarHarness.LoadAsync(repository.Path);
        harness.FocusRow("Branches");
        harness.Handler.TryHandle(ActionIds.SidebarFilter, null);
        foreach (var letter in "old") { harness.Handler.OnKey(new KeyEvent(letter.ToString())); }
        harness.Handler.TryHandle(ActionIds.InputAccept, null);

        harness.Handler.TryHandle(ActionIds.Cancel, null);
        Assert.Equal(string.Empty, harness.Tab.Sidebar.FilterText);
        Assert.Equal(PaneKind.Sidebar, harness.Tab.View.FocusedPane);

        harness.Handler.TryHandle(ActionIds.Cancel, null);
        Assert.Equal(PaneKind.Graph, harness.Tab.View.FocusedPane);
    }

    [Fact]
    public async Task GivenSidebarFocus_WhenGoingBack_ThenGraphGetsFocusInsteadOfQuitting()
    {
        using var repository = BranchyRepository();
        using var harness = await SidebarHarness.LoadAsync(repository.Path);
        harness.FocusRow("Branches");

        Assert.True(harness.Handler.TryHandle(ActionIds.NavigateBack, null));

        Assert.Equal(PaneKind.Graph, harness.Tab.View.FocusedPane);
    }

    [Fact]
    public async Task GivenLinkedWorktree_WhenLoadingTheTab_ThenEachBranchNamesTheWorktreeHoldingIt()
    {
        using var repository = BranchyRepository();
        var worktreePath = repository.AddWorktree("wt-feature", "feature");
        using var harness = await SidebarHarness.LoadAsync(repository.Path);

        var rows = harness.Source.Rows;

        var feature = rows.Single(row => row is { Kind: SidebarRowKind.LocalBranch, Label: "feature" });
        Assert.Equal(Path.GetFileName(worktreePath), feature.Annotation);
        Assert.False(feature.IsCurrent);
        var main = rows.Single(row => row is { Kind: SidebarRowKind.LocalBranch, Label: "main" });
        Assert.Equal(Path.GetFileName(repository.Path), main.Annotation);
        Assert.True(main.IsCurrent);
        Assert.Equal(string.Empty, rows.Single(row => row.Label == "old").Annotation);
        var worktrees = rows.Where(row => row.Kind == SidebarRowKind.Worktree).ToList();
        Assert.Equal(["repo", "wt-feature"], worktrees.Select(row => row.Label));
        Assert.Equal(["main", "feature"], worktrees.Select(row => row.Annotation));
        Assert.True(worktrees[0].IsCurrent);
    }

    [Fact]
    public async Task GivenTabOpenedOnALinkedWorktree_WhenLoading_ThenThatWorktreeIsTheCurrentOne()
    {
        using var repository = BranchyRepository();
        var worktreePath = repository.AddWorktree("wt-feature", "feature");
        using var harness = await SidebarHarness.LoadAsync(worktreePath);

        var current = harness.Source.Rows.Single(row => row is { Kind: SidebarRowKind.Worktree, IsCurrent: true });
        Assert.Equal("wt-feature", current.Label);
        Assert.True(harness.Source.Rows.Single(row => row is { Kind: SidebarRowKind.LocalBranch, Label: "feature" }).IsCurrent);
    }
}
