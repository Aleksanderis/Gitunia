using System.Drawing;
using System.Text;
using Gitunia.Core.Clipboard;
using Gitunia.Core.Diff;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git;
using Gitunia.Git.Diff.Models;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Tui.App;
using Gitunia.Tui.App.Clipboard;
using Gitunia.Tui.App.Panes;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Loading.Commits;
using Gitunia.Tui.Loading.Diff;
using Gitunia.Tui.Rendering.Details;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Views;
using Terminal.Gui.Views;
using Xunit;

namespace Gitunia.Tui.Tests.App.Panes;

public class DiffPaneControllerTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenAFileRow_WhenOpeningTheDiff_ThenTheDiffOpensFocusedAtThatFile()
    {
        using var harness = await DiffHarness.LoadAsync();
        harness.SelectFile("b.txt");

        harness.Execute(ActionIds.DetailOpenDiff);
        await harness.WaitForDiffAsync();

        Assert.True(harness.Session.Diff!.IsOpen);
        Assert.Equal(PaneKind.Diff, harness.Tab.View.FocusedPane);
        var document = harness.Session.Diff.Source.Document;
        Assert.Equal(document.FileRows[document.FileIndexOf("b.txt")], harness.DiffList.SelectedItem);
    }

    [Fact]
    public async Task GivenAnInfoRow_WhenOpeningTheDiff_ThenNothingOpensAndANoticeSaysWhy()
    {
        using var harness = await DiffHarness.LoadAsync();
        harness.DetailList.SelectedItem = 0;

        harness.Execute(ActionIds.DetailOpenDiff);

        Assert.False(harness.Session.Diff!.IsOpen);
        Assert.Equal("Select a file to see its diff", harness.Controller.Notice);
    }

    [Theory]
    [InlineData(BottomTab.Details)]
    [InlineData(BottomTab.Files)]
    public async Task GivenATabWithoutFileRows_WhenOpeningTheDiff_ThenNothingOpensAndTheNoticeNamesTheChangesTab(BottomTab tab)
    {
        using var harness = await DiffHarness.LoadAsync();
        harness.ShowTab(tab);
        harness.DetailList.SelectedItem = 0;

        harness.Execute(ActionIds.DetailOpenDiff);

        Assert.False(harness.Session.Diff!.IsOpen);
        Assert.Equal("Open the Changes tab (gc) to pick a file", harness.Controller.Notice);
    }

    [Fact]
    public async Task GivenAnOpenDiff_WhenJumpingBetweenHunksAndFiles_ThenTheCursorLandsOnTheirHeaders()
    {
        using var harness = await DiffHarness.LoadAsync();
        await harness.OpenAsync("a.txt");
        var document = harness.Session.Diff!.Source.Document;
        Assert.True(document.HunkRows.Count >= 2);
        Assert.True(document.FileRows.Count >= 2);

        harness.Execute(ActionIds.DiffNextHunk);
        var firstAfterStart = harness.DiffList.SelectedItem;
        harness.Execute(ActionIds.DiffNextHunk);
        harness.Execute(ActionIds.DiffPreviousHunk);
        var back = harness.DiffList.SelectedItem;
        harness.Execute(ActionIds.DiffNextFile);

        Assert.Contains(firstAfterStart!.Value, document.HunkRows);
        Assert.Equal(firstAfterStart, back);
        Assert.Equal(document.FileRows[1], harness.DiffList.SelectedItem);
    }

    [Fact]
    public async Task GivenTheLastHunk_WhenJumpingForward_ThenTheCursorStaysAndANoticeSaysSo()
    {
        using var harness = await DiffHarness.LoadAsync();
        await harness.OpenAsync("a.txt");
        harness.Execute(ActionIds.ListBottom);

        harness.Execute(ActionIds.DiffNextHunk);

        Assert.Equal(harness.Session.Diff!.Source.Count - 1, harness.DiffList.SelectedItem);
        Assert.Equal("No next hunk", harness.Controller.Notice);
    }

    [Fact]
    public async Task GivenAnOpenDiff_WhenSearching_ThenMatchesAreHighlightedAndNStepsThroughThem()
    {
        using var harness = await DiffHarness.LoadAsync();
        await harness.OpenAsync("a.txt");

        harness.Execute(ActionIds.DiffSearchOpen);
        Assert.Equal(KeyMode.Insert, harness.Mode);
        harness.Type("line");
        Assert.StartsWith("/line_", harness.Controller.StatusLine, StringComparison.Ordinal);
        harness.Execute(ActionIds.InputAccept);
        var first = harness.DiffList.SelectedItem;
        harness.Execute(ActionIds.DiffSearchNext);
        var second = harness.DiffList.SelectedItem;

        Assert.Equal(KeyMode.Normal, harness.Mode);
        Assert.Equal("line", harness.Session.Diff!.Source.Highlight);
        Assert.True(second > first);
    }

    [Fact]
    public async Task GivenAnActiveSearch_WhenGoingBack_ThenTheSearchClearsBeforeThePaneCloses()
    {
        using var harness = await DiffHarness.LoadAsync();
        await harness.OpenAsync("a.txt");
        harness.Execute(ActionIds.DiffSearchOpen);
        harness.Type("line");
        harness.Execute(ActionIds.InputAccept);

        harness.Execute(ActionIds.NavigateBack);

        Assert.True(harness.Session.Diff!.IsOpen);
        Assert.Null(harness.Session.Diff.Source.Highlight);
        harness.Execute(ActionIds.NavigateBack);
        Assert.False(harness.Session.Diff.IsOpen);
        Assert.Equal(PaneKind.Detail, harness.Tab.View.FocusedPane);
        Assert.Empty(harness.Session.Diff.Source.Document.Rows);
    }

    [Fact]
    public async Task GivenAnOpenSearchBox_WhenCancelling_ThenTheModeIsNormalAgain()
    {
        using var harness = await DiffHarness.LoadAsync();
        await harness.OpenAsync("a.txt");
        harness.Execute(ActionIds.DiffSearchOpen);
        harness.Type("x");

        harness.Execute(ActionIds.Cancel);

        Assert.Equal(KeyMode.Normal, harness.Mode);
        Assert.Equal(string.Empty, harness.Controller.StatusLine);
        Assert.True(harness.Session.Diff!.IsOpen);
    }

    [Fact]
    public async Task GivenAShortScreen_WhenOpeningTheDiff_ThenItOpensZoomedAndFocusLeavingEndsTheZoom()
    {
        using var harness = await DiffHarness.LoadAsync(screenHeight: 10);

        await harness.OpenAsync("a.txt");

        Assert.Equal(PaneKind.Diff, harness.Controller.Arrangement().Zoomed);
        harness.Send(new FocusPaneCommand(PaneKind.Detail));
        Assert.Null(harness.Controller.Arrangement().Zoomed);
        harness.Send(new FocusPaneCommand(PaneKind.Diff));
        Assert.Null(harness.Controller.Arrangement().Zoomed);
    }

    [Fact]
    public async Task GivenATallScreen_WhenOpeningTheDiff_ThenItIsNotZoomed()
    {
        using var harness = await DiffHarness.LoadAsync(screenHeight: 40);

        await harness.OpenAsync("a.txt");

        Assert.Null(harness.Controller.Arrangement().Zoomed);
    }

    [Fact]
    public async Task GivenAWideScreenOnChanges_WhenTheDetailsHaveArrived_ThenTheDiffOfTheFirstFileIsShownWithoutOpeningIt()
    {
        using var harness = await DiffHarness.LoadAsync(screenWidth: 100);
        harness.SelectFirstFile();

        harness.Controller.FollowSelection();
        await harness.WaitForDiffAsync();

        Assert.True(harness.Controller.Arrangement().IsDiffOpen);
        Assert.False(harness.Session.Diff!.IsOpen);
        var document = harness.Session.Diff.Source.Document;
        Assert.Equal(document.FileRows[0], harness.DiffList.SelectedItem);
    }

    [Fact]
    public async Task GivenAShownDiff_WhenTheFileCursorMoves_ThenTheDiffShowsThatFileAtOnceWithoutLoadingAgain()
    {
        using var harness = await DiffHarness.LoadAsync(screenWidth: 100);
        harness.SelectFirstFile();
        harness.Controller.FollowSelection();
        await harness.WaitForDiffAsync();
        var loaded = harness.Session.Diff!.Source.Document;

        harness.SelectFile("b.txt");

        Assert.Same(loaded, harness.Session.Diff.Source.Document);
        Assert.Equal(loaded.FileRows[loaded.FileIndexOf("b.txt")], harness.DiffList.SelectedItem);
    }

    [Theory]
    [InlineData(BottomTab.Details)]
    [InlineData(BottomTab.Files)]
    public async Task GivenAWideScreen_WhenAnotherTabIsShown_ThenTheDiffIsNotShown(BottomTab tab)
    {
        using var harness = await DiffHarness.LoadAsync(screenWidth: 100);

        harness.Send(new SetBottomTabCommand(tab));

        Assert.False(harness.Controller.Arrangement().IsDiffOpen);
    }

    [Fact]
    public async Task GivenANarrowScreen_WhenOnChanges_ThenTheDiffStaysClosedUntilOpened()
    {
        using var harness = await DiffHarness.LoadAsync(screenWidth: 71);
        harness.SelectFirstFile();

        harness.Controller.FollowSelection();

        Assert.False(harness.Controller.Arrangement().IsDiffOpen);
        Assert.Equal(DiffStatus.None, harness.Session.Diff!.Store.Status);
    }

    [Fact]
    public async Task GivenAShownDiff_WhenEnterIsPressedAndThenBack_ThenFocusMovesToTheDiffAndBackAndTheDiffStays()
    {
        using var harness = await DiffHarness.LoadAsync(screenWidth: 100);
        harness.SelectFile("b.txt");
        harness.Controller.FollowSelection();
        await harness.WaitForDiffAsync();

        harness.Execute(ActionIds.DetailOpenDiff);

        Assert.Equal(PaneKind.Diff, harness.Tab.View.FocusedPane);
        Assert.False(harness.Session.Diff!.IsOpen);
        harness.Execute(ActionIds.NavigateBack);
        Assert.Equal(PaneKind.Detail, harness.Tab.View.FocusedPane);
        Assert.True(harness.Controller.Arrangement().IsDiffOpen);
        Assert.Equal(DiffStatus.Loaded, harness.Session.Diff.Store.Status);
    }

    [Fact]
    public async Task GivenTheDiffHasFocus_WhenJumpingToTheNextFile_ThenTheFileListFollowsWithoutMovingTheDiffAway()
    {
        using var harness = await DiffHarness.LoadAsync(screenWidth: 100);
        harness.SelectFirstFile();
        harness.Controller.FollowSelection();
        await harness.WaitForDiffAsync();
        harness.Execute(ActionIds.DetailOpenDiff);
        var document = harness.Session.Diff!.Source.Document;

        harness.Execute(ActionIds.DiffNextFile);

        Assert.Equal(document.FileRows[1], harness.DiffList.SelectedItem);
        Assert.Equal(harness.Session.DetailSource.Content.FirstFileRow + 1, harness.DetailList.SelectedItem);
    }

    [Fact]
    public async Task GivenTheDiffHasFocus_WhenTheChangesTabIsLeft_ThenFocusReturnsToTheDetailPane()
    {
        using var harness = await DiffHarness.LoadAsync(screenWidth: 100);
        harness.SelectFirstFile();
        harness.Controller.FollowSelection();
        await harness.WaitForDiffAsync();
        harness.Execute(ActionIds.DetailOpenDiff);

        harness.Send(new SetBottomTabCommand(BottomTab.Details));

        Assert.Equal(PaneKind.Detail, harness.Tab.View.FocusedPane);
    }

    [Fact]
    public async Task GivenAShownDiff_WhenAnotherCommitIsSelected_ThenTheDiffLoadsItAtItsFirstFile()
    {
        using var harness = await DiffHarness.LoadAsync(screenWidth: 100);
        harness.SelectFile("b.txt");
        harness.Controller.FollowSelection();
        await harness.WaitForDiffAsync();
        var second = harness.Session.Graph[1].Id;

        await harness.ShowCommitAsync(1);
        harness.Controller.FollowSelection();
        await harness.WaitForDiffAsync();

        Assert.Equal(second, harness.Session.Diff!.Store.Commit);
        var document = harness.Session.Diff.Source.Document;
        Assert.Equal(document.FileRows[0], harness.DiffList.SelectedItem);
    }

    [Fact]
    public async Task GivenAnOnDemandDiffOnANarrowScreen_WhenTheFileCursorResetsOnANewCommit_ThenTheDiffKeepsItsFile()
    {
        using var harness = await DiffHarness.LoadAsync(screenWidth: 60);
        await harness.OpenAsync("b.txt");

        await harness.ShowCommitAsync(1);
        harness.Controller.FollowSelection();
        await harness.WaitForDiffAsync();

        Assert.Equal(harness.Session.Graph[1].Id, harness.Session.Diff!.Store.Commit);
    }

    [Fact]
    public async Task GivenANarrowScreenWithTheDiffNotLoaded_WhenTheScreenGrowsWide_ThenTheDiffLoadsWithoutAKey()
    {
        using var harness = await DiffHarness.LoadAsync(screenWidth: 60);
        harness.SelectFirstFile();

        harness.ScreenWidth = 100;
        harness.Controller.OnScreenResized();
        await harness.WaitForDiffAsync();

        Assert.True(harness.Controller.Arrangement().IsDiffOpen);
    }

    [Fact]
    public async Task GivenTheDiffHasFocus_WhenTheScreenShrinksBelowSideBySide_ThenFocusReturnsToTheFileList()
    {
        using var harness = await DiffHarness.LoadAsync(screenWidth: 100);
        harness.SelectFirstFile();
        harness.Controller.FollowSelection();
        await harness.WaitForDiffAsync();
        harness.Execute(ActionIds.DetailOpenDiff);

        harness.ScreenWidth = 60;
        harness.Controller.OnScreenResized();

        Assert.Equal(PaneKind.Detail, harness.Tab.View.FocusedPane);
    }

    [Fact]
    public async Task GivenAWideTabWhoseDiffLoadedInTheBackground_WhenItIsShownAgain_ThenTheLoadedDiffIsApplied()
    {
        using var harness = await DiffHarness.LoadAsync(screenWidth: 100);
        harness.SelectFirstFile();
        harness.Controller.FollowSelection();
        await harness.WaitForDiffAsync();
        harness.Controller.OnTabSwitched(harness.Session, null);
        harness.Session.Diff!.Source.Update(DiffDocument.Empty);

        harness.Controller.OnTabSwitched(null, harness.Session);

        Assert.Same(harness.Session.Diff.Store.Document, harness.Session.Diff.Source.Document);
    }

    [Fact]
    public async Task GivenAZoomedPane_WhenAskingForTheArrangement_ThenNothingChanges()
    {
        using var harness = await DiffHarness.LoadAsync();
        await harness.OpenAsync("a.txt");
        harness.Execute(ActionIds.PaneZoom);

        // A zoom that does not match the focus is hidden by the arrangement but only released by the workspace change.
        harness.Session.Diff!.ZoomedPane = PaneKind.Graph;

        Assert.Null(harness.Controller.Arrangement().Zoomed);
        Assert.Equal(PaneKind.Graph, harness.Session.Diff.ZoomedPane);
    }

    [Fact]
    public async Task GivenASearchBeingTyped_WhenAClickMovesFocusToAnotherPane_ThenTheSearchEndsAndTheModeIsNormal()
    {
        using var harness = await DiffHarness.LoadAsync();
        await harness.OpenAsync("a.txt");
        harness.Execute(ActionIds.DiffSearchOpen);
        harness.Type("line");
        Assert.Equal(KeyMode.Insert, harness.Mode);

        harness.Send(new FocusPaneCommand(PaneKind.Graph));

        Assert.Equal(KeyMode.Normal, harness.Mode);
        Assert.Equal(string.Empty, harness.Controller.StatusLine);
        Assert.Null(harness.Session.Diff!.Source.Highlight);
    }

    [Fact]
    public async Task GivenAnAcceptedSearch_WhenAClickMovesFocusToAnotherPane_ThenTheSearchStays()
    {
        using var harness = await DiffHarness.LoadAsync();
        await harness.OpenAsync("a.txt");
        harness.Execute(ActionIds.DiffSearchOpen);
        harness.Type("line");
        harness.Execute(ActionIds.InputAccept);

        harness.Send(new FocusPaneCommand(PaneKind.Graph));

        Assert.Equal("line", harness.Session.Diff!.Source.Highlight);
    }

    [Fact]
    public async Task GivenASearchBeingTyped_WhenTheTabChanges_ThenTheSearchEndsAndTheModeIsNormal()
    {
        using var harness = await DiffHarness.LoadAsync();
        await harness.OpenAsync("a.txt");
        harness.Execute(ActionIds.DiffSearchOpen);
        harness.Type("line");

        harness.Controller.OnTabSwitched(harness.Session, null);

        Assert.Equal(KeyMode.Normal, harness.Mode);
        Assert.False(harness.Session.Diff!.Search.IsOpen);
        Assert.Null(harness.Session.Diff.Source.Highlight);
    }

    [Fact]
    public async Task GivenWrapOn_WhenThePaneWidthChangesWithoutAKey_ThenTheCursorStaysOnItsRow()
    {
        using var testRepository = TestRepository.Create().Commit("long", new TestFile("a.txt", $"{new string('x', 150)}\nshort\n"));
        using var harness = await DiffHarness.LoadAsync(testRepository);
        await harness.OpenAsync("a.txt");
        var source = harness.Session.Diff!.Source;
        harness.Execute(ActionIds.DiffToggleWrap);
        harness.Execute(ActionIds.ListBottom);
        var row = source.RowOf(harness.DiffList.SelectedItem!.Value);

        harness.DiffList.Width = 30;
        harness.DiffList.SetRelativeLayout(new Size(80, 24));

        Assert.Equal(28, source.Width);
        Assert.Equal(row, source.RowOf(harness.DiffList.SelectedItem!.Value));
    }

    [Fact]
    public async Task GivenAFocusedPane_WhenZooming_ThenItFillsTheScreenUntilZoomedAgainOrFocusLeaves()
    {
        using var harness = await DiffHarness.LoadAsync();
        await harness.OpenAsync("a.txt");

        harness.Execute(ActionIds.PaneZoom);
        Assert.Equal(PaneKind.Diff, harness.Controller.Arrangement().Zoomed);
        harness.Execute(ActionIds.PaneZoom);
        Assert.Null(harness.Controller.Arrangement().Zoomed);
        harness.Execute(ActionIds.PaneZoom);
        harness.Send(new FocusPaneCommand(PaneKind.Graph));

        Assert.Null(harness.Controller.Arrangement().Zoomed);
        Assert.True(harness.Controller.Arrangement().IsDiffOpen);
    }

    [Fact]
    public async Task GivenTheSidebarIsFocused_WhenZooming_ThenANoticeSaysItCannot()
    {
        using var harness = await DiffHarness.LoadAsync();
        harness.Send(new FocusPaneCommand(PaneKind.Sidebar));

        harness.Execute(ActionIds.PaneZoom);

        Assert.Equal("The sidebar cannot be zoomed", harness.Controller.Notice);
        Assert.Null(harness.Controller.Arrangement().Zoomed);
    }

    [Fact]
    public async Task GivenAMergeCommitSelected_WhenOpeningAFile_ThenTheFirstParentDiffShows()
    {
        using var testRepository = TestRepository.Create()
            .Commit("root", new TestFile("base.txt", "base\n"))
            .CheckoutNewBranch("side")
            .Commit("side work", new TestFile("side.txt", "side\n"))
            .Checkout("main")
            .Commit("main work", new TestFile("main.txt", "main\n"))
            .Merge("side");
        using var harness = await DiffHarness.LoadAsync(testRepository);
        harness.SelectFile("side.txt");

        harness.Execute(ActionIds.DetailOpenDiff);
        await harness.WaitForDiffAsync();

        Assert.Equal(["side.txt"], harness.Session.Diff!.Source.Document.FilePaths.Select(names => names[0]));
    }

    [Fact]
    public async Task GivenTwoTabs_WhenSwitchingAway_ThenTheDiffCursorIsRestored()
    {
        using var harness = await DiffHarness.LoadAsync();
        await harness.OpenAsync("a.txt");
        harness.Execute(ActionIds.DiffNextHunk);
        var row = harness.DiffList.SelectedItem;

        harness.Controller.OnTabSwitched(harness.Session, null);
        Assert.Null(harness.DiffList.Source);
        harness.Controller.OnTabSwitched(null, harness.Session);

        Assert.Equal(row, harness.DiffList.SelectedItem);
        Assert.Same(harness.Session.Diff!.Source, harness.DiffList.Source);
    }

    [Fact]
    public async Task GivenADiffThatLoadsWhileAnotherTabIsShown_WhenSwitchingBack_ThenItShowsTheLoadedDiffAtTheFile()
    {
        using var harness = await DiffHarness.LoadAsync();
        harness.SelectFile("b.txt");
        harness.Execute(ActionIds.DetailOpenDiff);
        harness.Controller.OnTabSwitched(harness.Session, null);
        await harness.WaitForDiffAsync();

        harness.Controller.OnTabSwitched(null, harness.Session);

        var document = harness.Session.Diff!.Source.Document;
        Assert.Equal(DiffStatus.Loaded, harness.Session.Diff.Store.Status);
        Assert.Equal(document.FileRows[document.FileIndexOf("b.txt")], harness.DiffList.SelectedItem);
    }

    [Fact]
    public async Task GivenALongLine_WhenTogglingWrap_ThenItTakesMoreLinesAndTheCursorKeepsItsRow()
    {
        using var testRepository = TestRepository.Create().Commit("long", new TestFile("a.txt", $"{new string('x', 150)}\n"));
        using var harness = await DiffHarness.LoadAsync(testRepository);
        await harness.OpenAsync("a.txt");
        var source = harness.Session.Diff!.Source;
        var rows = source.Document.Rows.Count;
        harness.Execute(ActionIds.ListBottom);
        var row = source.RowOf(harness.DiffList.SelectedItem!.Value);

        harness.Execute(ActionIds.DiffToggleWrap);

        Assert.True(source.IsWrapped);
        Assert.Equal(rows + 3, source.Count);
        Assert.Equal(row, source.RowOf(harness.DiffList.SelectedItem!.Value));
        Assert.Equal("Word wrap on", harness.Controller.Notice);
        harness.Execute(ActionIds.DiffToggleWrap);
        Assert.False(source.IsWrapped);
        Assert.Equal(rows, source.Count);
        Assert.Equal(row, harness.DiffList.SelectedItem);
    }

    [Fact]
    public async Task GivenWrapOn_WhenThePaneWidthChanges_ThenTheCursorStaysOnItsRow()
    {
        using var testRepository = TestRepository.Create().Commit("long", new TestFile("a.txt", $"{new string('x', 150)}\nshort\n"));
        using var harness = await DiffHarness.LoadAsync(testRepository);
        await harness.OpenAsync("a.txt");
        var source = harness.Session.Diff!.Source;
        harness.Execute(ActionIds.DiffToggleWrap);
        harness.Execute(ActionIds.ListBottom);
        var row = source.RowOf(harness.DiffList.SelectedItem!.Value);

        harness.DiffList.Width = 30;
        harness.DiffList.SetRelativeLayout(new Size(80, 24));
        harness.Execute(ActionIds.ListUp);
        harness.Execute(ActionIds.ListDown);

        Assert.Equal(row, source.RowOf(harness.DiffList.SelectedItem!.Value));
        Assert.True(source.Count > source.Document.Rows.Count + 4);
    }

    [Fact]
    public async Task GivenTwoQuickSelectionChanges_WhenFollowing_ThenTheRememberedFileSurvives()
    {
        using var harness = await DiffHarness.LoadAsync();
        await harness.OpenAsync("b.txt");
        var store = harness.Session.Diff!.Store;

        await harness.SelectCommitAsync(harness.Session.Graph[1].Id);
        harness.Controller.FollowSelection();
        await harness.SelectCommitAsync(harness.Session.Graph[0].Id);
        harness.Controller.FollowSelection();
        await harness.WaitForDiffAsync();

        Assert.Equal(harness.Session.Graph[0].Id, store.Commit);
        var document = harness.Session.Diff.Source.Document;
        Assert.Equal(document.FileRows[document.FileIndexOf("b.txt")], harness.DiffList.SelectedItem);
    }

    [Fact]
    public async Task GivenACutDiff_WhenLoadingAnyway_ThenTheWholeDiffLoadsAndTheFileKeepsTheCursor()
    {
        using var harness = await DiffHarness.LoadAsync(rowLimit: 8);
        await harness.OpenAsync("a.txt");
        var cutRows = harness.Session.Diff!.Source.Document.Rows.Count;
        Assert.True(harness.Session.Diff.Store.IsCut);

        harness.Execute(ActionIds.DiffLoadAnyway);
        await harness.WaitForDiffAsync();

        Assert.False(harness.Session.Diff.Store.IsCut);
        Assert.True(harness.Session.Diff.Source.Document.Rows.Count > cutRows);
        Assert.Equal(0, harness.DiffList.SelectedItem);
    }

    [Fact]
    public async Task GivenAnOpenDiff_WhenChangingContextAndTogglingOptions_ThenTheDiffLoadsAgainWithThem()
    {
        using var harness = await DiffHarness.LoadAsync();
        await harness.OpenAsync("a.txt");
        var store = harness.Session.Diff!.Store;

        harness.Execute(ActionIds.DiffMoreContext);
        await harness.WaitForDiffAsync();
        harness.Execute(ActionIds.DiffToggleIgnoreWhitespace);
        await harness.WaitForDiffAsync();
        harness.Execute(ActionIds.DiffToggleWholeFile);
        await harness.WaitForDiffAsync();

        Assert.Equal(new DiffOptions(DiffOptions.DefaultContextLines + DiffOptions.ContextStep, true, true), store.Options);
        Assert.Equal(DiffStatus.Loaded, store.Status);
        Assert.Equal("Diff: whole files, whitespace ignored", harness.Controller.Notice);

        harness.Execute(ActionIds.DiffToggleWholeFile);
        await harness.WaitForDiffAsync();
        harness.Execute(ActionIds.DiffLessContext);
        await harness.WaitForDiffAsync();

        Assert.Equal(new DiffOptions(DiffOptions.DefaultContextLines, false, true), store.Options);
        Assert.Equal(0, harness.DiffList.SelectedItem);
    }

    [Fact]
    public async Task GivenWholeFileMode_WhenJumpingBetweenHunks_ThenEachGroupOfChangesIsItsOwnHunk()
    {
        using var harness = await DiffHarness.LoadAsync();
        await harness.OpenAsync("a.txt");
        var unified = harness.Session.Diff!.Source.Document.HunkRows.Count;

        harness.Execute(ActionIds.DiffToggleWholeFile);
        await harness.WaitForDiffAsync();
        var source = harness.Session.Diff.Source;
        var hunks = source.Document.HunkRows;

        Assert.Equal(unified, hunks.Count);
        Assert.Equal(3, hunks.Count);
        harness.Execute(ActionIds.DiffNextHunk);
        Assert.Equal(hunks[0], source.RowOf(harness.DiffList.SelectedItem!.Value));
        harness.Execute(ActionIds.DiffNextHunk);
        Assert.Equal(hunks[1], source.RowOf(harness.DiffList.SelectedItem!.Value));
        harness.Execute(ActionIds.DiffPreviousHunk);
        Assert.Equal(hunks[0], source.RowOf(harness.DiffList.SelectedItem!.Value));
    }

    [Fact]
    public async Task GivenAWidePane_WhenTogglingSideBySide_ThenRemovedAndAddedRowsShareALineAndTheCursorStaysOnItsRow()
    {
        using var harness = await DiffHarness.LoadAsync();
        await harness.OpenAsync("a.txt");
        harness.DiffList.Width = 100;
        harness.DiffList.SetRelativeLayout(new Size(120, 24));
        var source = harness.Session.Diff!.Source;
        var rows = source.Document.Rows.Count;
        var removed = source.Document.Rows.Index().First(item => item.Item.Kind == DiffRowKind.Removed).Index;
        harness.DiffList.SelectedItem = source.LineOf(removed);

        harness.Execute(ActionIds.DiffToggleSideBySide);

        Assert.True(source.ShowsSideBySide);
        Assert.True(source.Count < rows);
        Assert.Equal(source.LineOf(removed), source.LineOf(removed + 1));
        Assert.Equal(removed, source.RowOf(harness.DiffList.SelectedItem!.Value));
        Assert.Equal("Side by side on", harness.Controller.Notice);
        harness.Execute(ActionIds.DiffToggleSideBySide);
        Assert.Equal(rows, source.Count);
        Assert.Equal("Side by side off", harness.Controller.Notice);
    }

    [Fact]
    public async Task GivenANarrowPane_WhenTogglingSideBySide_ThenTheUnifiedRowsStayAndANoticeNamesTheWidth()
    {
        using var harness = await DiffHarness.LoadAsync();
        await harness.OpenAsync("a.txt");
        var source = harness.Session.Diff!.Source;
        var rows = source.Document.Rows.Count;

        harness.Execute(ActionIds.DiffToggleSideBySide);

        Assert.True(source.IsSideBySide);
        Assert.False(source.ShowsSideBySide);
        Assert.Equal(rows, source.Count);
        Assert.Contains("80", harness.Controller.Notice, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenNoContextLeft_WhenShowingFewerLines_ThenANoticeSaysSo()
    {
        using var harness = await DiffHarness.LoadAsync();
        await harness.OpenAsync("a.txt");

        harness.Execute(ActionIds.DiffLessContext);
        await harness.WaitForDiffAsync();
        harness.Execute(ActionIds.DiffLessContext);

        Assert.Equal("Context is already at its limit", harness.Controller.Notice);
    }

    [Fact]
    public async Task GivenAnUncutDiff_WhenLoadingAnyway_ThenANoticeSaysNothingWasCut()
    {
        using var harness = await DiffHarness.LoadAsync();
        await harness.OpenAsync("a.txt");

        harness.Execute(ActionIds.DiffLoadAnyway);

        Assert.Equal("Nothing was cut short", harness.Controller.Notice);
    }

    [Fact]
    public async Task GivenAnOpenDiff_WhenTheSelectedCommitChanges_ThenTheDiffFollowsAndKeepsTheFile()
    {
        using var harness = await DiffHarness.LoadAsync();
        await harness.OpenAsync("b.txt");
        var shown = harness.Session.Diff!.Store.Commit;

        await harness.SelectCommitAsync(harness.Session.Graph[1].Id);
        harness.Controller.FollowSelection();
        await harness.WaitForDiffAsync();

        Assert.NotEqual(shown, harness.Session.Diff.Store.Commit);
        Assert.Equal(harness.Session.Graph[1].Id, harness.Session.Diff.Store.Commit);
        var document = harness.Session.Diff.Source.Document;
        Assert.Equal(document.FileRows[document.FileIndexOf("b.txt")], harness.DiffList.SelectedItem);
    }

    [Fact]
    public async Task GivenAClosedDiff_WhenTheSelectedCommitChanges_ThenNothingLoads()
    {
        using var harness = await DiffHarness.LoadAsync();

        await harness.SelectCommitAsync(harness.Session.Graph[1].Id);
        harness.Controller.FollowSelection();

        Assert.Equal(DiffStatus.None, harness.Session.Diff!.Store.Status);
    }

    [Fact]
    public async Task GivenAnAddedRow_WhenCopyingTheLine_ThenItGoesToTheClipboardWithItsPlusPrefix()
    {
        using var harness = await DiffHarness.LoadAsync();
        await harness.OpenAsync("a.txt");
        harness.SelectFirstRowOfKind(DiffRowKind.Added);

        harness.Execute(ActionIds.DiffCopyLine);

        Assert.Equal("+line 3 changed", harness.CopiedText());
    }

    [Fact]
    public async Task GivenACursorInAFile_WhenCopyingTheFile_ThenOnlyThatFilesRowsAreCopied()
    {
        using var harness = await DiffHarness.LoadAsync();
        await harness.OpenAsync("a.txt");
        var document = harness.Session.Diff!.Source.Document;
        harness.DiffList.SelectedItem = document.FileRows[document.FileIndexOf("b.txt")];

        harness.Execute(ActionIds.DiffCopyFile);

        var copied = harness.CopiedText();
        Assert.Contains("-b\n", copied, StringComparison.Ordinal);
        Assert.Contains("+b changed\n", copied, StringComparison.Ordinal);
        Assert.DoesNotContain("line 3", copied, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenACursorAboveTheFirstFile_WhenCopyingTheFile_ThenANoticeSaysItIsNotInAFile()
    {
        using var harness = await DiffHarness.LoadAsync();
        await harness.OpenAsync("a.txt");
        harness.ShowWarningOnly("parser warning");

        harness.Execute(ActionIds.DiffCopyFile);

        Assert.Equal("The cursor is not in a file", harness.Controller.Notice);
        Assert.Equal(string.Empty, harness.Clipboard.ToString());
    }

    [Fact]
    public async Task GivenAWarningRow_WhenCopyingTheLine_ThenNothingIsCopied()
    {
        using var harness = await DiffHarness.LoadAsync();
        await harness.OpenAsync("a.txt");
        harness.ShowWarningOnly("parser warning");

        harness.Execute(ActionIds.DiffCopyLine);

        Assert.Equal("Nothing to copy", harness.Controller.Notice);
        Assert.Equal(string.Empty, harness.Clipboard.ToString());
    }

    [Fact]
    public async Task GivenWrapOnAndTheCursorOnAContinuationLine_WhenCopyingTheLine_ThenTheWholeRowIsCopied()
    {
        using var testRepository = TestRepository.Create().Commit("long", new TestFile("a.txt", $"{new string('x', 150)}\n"));
        using var harness = await DiffHarness.LoadAsync(testRepository);
        await harness.OpenAsync("a.txt");
        harness.Execute(ActionIds.DiffToggleWrap);
        harness.Execute(ActionIds.ListBottom);
        var source = harness.Session.Diff!.Source;

        // The 150-character line wraps in the 60-wide pane, so the bottom item is a continuation line.
        Assert.True(harness.DiffList.SelectedItem > source.LineOf(source.RowOf(harness.DiffList.SelectedItem!.Value)));

        harness.Execute(ActionIds.DiffCopyLine);

        Assert.Equal($"+{new string('x', 150)}", harness.CopiedText());
    }

    private sealed class DiffHarness : IDisposable
    {
        private readonly TestRepository? _ownedRepository;
        private readonly UiPump _pump;
        private readonly KeyHolder _keys = new();

        private DiffHarness(WorkspaceHost host, TabSession session, UiPump pump, TestRepository? ownedRepository, int screenHeight, int screenWidth)
        {
            _pump = pump;
            Host = host;
            Session = session;
            _ownedRepository = ownedRepository;
            ScreenWidth = screenWidth;
            DetailList = new ListView { Source = session.DetailSource };
            DiffList = new ListView { X = 0, Y = 0, Width = 60, Height = 20 };
            DiffList.SetRelativeLayout(new Size(80, 24));
            Controller = new DiffPaneController(host, () => Session, DetailList, DiffList, mode => _keys.Mode = mode, () => new ScreenSize(ScreenWidth, screenHeight), () => { }, () => { }, new ClipboardCopier(new ClipboardWriter(Clipboard, new Osc52Options(Osc52Mode.Direct, 100_000)), new StatusChannel()));
            Controller.OnTabSwitched(null, session);
            DetailList.ValueChanged += (_, _) => Controller.OnDetailCursorMoved();
        }

        /// <summary>The terminal width the controller sees; tests change it to simulate a resize.</summary>
        public int ScreenWidth { get; set; }

        public WorkspaceHost Host { get; }

        public TabSession Session { get; }

        public ListView DetailList { get; }

        public ListView DiffList { get; }

        public DiffPaneController Controller { get; }

        /// <summary>What the controller wrote to the terminal (OSC 52 sequences).</summary>
        public StringWriter Clipboard { get; } = new();

        public KeyMode Mode => _keys.Mode;

        public RepositoryState Tab => Host.Workspace.ActiveTab!;

        public static async Task<DiffHarness> LoadAsync(TestRepository? testRepository = null, int rowLimit = DiffStore.DefaultRowLimit, int screenHeight = 40, int screenWidth = 60)
        {
            var owned = testRepository is null ? CreateDefaultRepository() : null;
            var path = (testRepository ?? owned)!.WorkingTreePath;
            var location = await TestGit.Services.Discovery.DiscoverAsync(path, Token);
            var host = new WorkspaceHost(Workspace.Empty);
            host.Dispatch(new OpenRepositoryCommand(location));
            var tabId = host.Workspace.ActiveTabId!.Value;
            var pump = new UiPump(Token);
            var session = new TabSession(
                tabId,
                GraphGlyphSet.Unicode,
                new CommitDetailsStore(TestGit.Services.CommitDetails, location, pump.Post, TimeSpan.Zero),
                new DiffStore(TestGit.Services.Diff, location, pump.Post, rowLimit));
            await TestGit.Services.RepositoryLoader.RunAsync(location, session, host, new StatusChannel(), InlineUi.Post, session.Cancellation.Token);
            var harness = new DiffHarness(host, session, pump, owned, screenHeight, screenWidth);
            await harness.ShowDetailsOfAsync(session.Graph[0].Id);
            return harness;
        }

        /// <summary>The text of the last OSC 52 copy, decoded.</summary>
        public string CopiedText()
        {
            var sequence = Clipboard.ToString();
            const string prefix = "\u001b]52;c;";
            Assert.StartsWith(prefix, sequence, StringComparison.Ordinal);
            var payload = sequence[prefix.Length..sequence.IndexOf('\u001b', prefix.Length)];
            return Encoding.UTF8.GetString(Convert.FromBase64String(payload));
        }

        public void SelectFirstRowOfKind(DiffRowKind kind)
        {
            var rows = Session.Diff!.Source.Document.Rows;
            DiffList.SelectedItem = rows.ToList().FindIndex(row => row.Kind == kind);
        }

        /// <summary>Swaps the shown document for one that only holds a parser warning row (the rows above the first file).</summary>
        public void ShowWarningOnly(string warning)
        {
            Session.Diff!.Source.Update(new DiffDocumentBuilder().Build([warning]));
            DiffList.SelectedItem = 0;
        }

        public void Send(RepositoryCommand command) => Host.Dispatch(new TabCommand(Tab.Id, command));

        public void Execute(ActionId action, int? count = null) => Controller.TryExecute(action, count);

        public void Type(string text)
        {
            foreach (var character in text)
            {
                Controller.HandleTextKey(new KeyEvent(character.ToString()));
            }
        }

        public void ShowTab(BottomTab tab)
        {
            Session.DetailSource.Update(DetailContentBuilder.Build(Session.Graph[0], Session.Details.Current, tab));
            Send(new FocusPaneCommand(PaneKind.Detail));
        }

        public void SelectFile(string path)
        {
            Send(new FocusPaneCommand(PaneKind.Detail));
            var content = Session.DetailSource.Content;
            var index = content.Files.ToList().FindIndex(file => file.Path == path);
            Assert.True(index >= 0, $"{path} is not in the file list");
            DetailList.SelectedItem = content.FirstFileRow + index;
        }

        public void SelectFirstFile()
        {
            Send(new FocusPaneCommand(PaneKind.Detail));
            DetailList.SelectedItem = Session.DetailSource.Content.FirstFileRow;
        }

        /// <summary>Shows the commit's changes in the file list the way the application does, with the cursor on its first file.</summary>
        public async Task ShowCommitAsync(int graphIndex)
        {
            var entry = Session.Graph[graphIndex];
            await SelectCommitAsync(entry.Id);
            Session.DetailSource.Update(DetailContentBuilder.Build(entry, Session.Details.Current, BottomTab.Changes));
            DetailList.SelectedItem = Session.DetailSource.Content.FirstFileRow;
        }

        public async Task OpenAsync(string path)
        {
            SelectFile(path);
            Execute(ActionIds.DetailOpenDiff);
            await WaitForDiffAsync();
        }

        public async Task WaitForDiffAsync()
        {
            await _pump.RunUntilAsync(() => Session.Diff!.Store.Status == DiffStatus.Loaded);
        }

        public async Task SelectCommitAsync(ObjectId commit)
        {
            Session.Details.Select(commit);
            await _pump.RunUntilAsync(() => Session.Details.Current.Status == CommitDetailsStatus.Loaded && Session.Details.Current.Commit == commit);
        }

        public void Dispose()
        {
            Session.Dispose();
            _ownedRepository?.Dispose();
        }

        private static TestRepository CreateDefaultRepository() => TestRepository.Create()
            .Commit("root", new TestFile("a.txt", string.Concat(Enumerable.Range(1, 40).Select(index => $"line {index}\n"))), new TestFile("b.txt", "b\n"))
            .Commit(
                "edit",
                new TestFile("a.txt", string.Concat(Enumerable.Range(1, 40).Select(index => index is 3 or 38 ? $"line {index} changed\n" : $"line {index}\n"))),
                new TestFile("b.txt", "b changed\n"));

        private async Task ShowDetailsOfAsync(ObjectId commit)
        {
            Session.Details.Select(commit);
            await _pump.RunUntilAsync(() => Session.Details.Current.Status == CommitDetailsStatus.Loaded);

            var entry = Session.Graph[0];
            Session.DetailSource.Update(DetailContentBuilder.Build(entry, Session.Details.Current, BottomTab.Changes));
            Send(new FocusPaneCommand(PaneKind.Detail));
        }
    }

    private sealed class KeyHolder
    {
        public KeyMode Mode { get; set; } = KeyMode.Normal;
    }
}
