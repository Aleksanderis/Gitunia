using System.Globalization;
using Gitunia.Core.Diff;
using Gitunia.Core.DiffPane;
using Gitunia.Core.Effects;
using Gitunia.Core.FileTree;
using Gitunia.Core.Keymap;
using Gitunia.Core.Shell;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git;
using Gitunia.Git.Diff;
using Gitunia.Git.Diff.Models;
using Gitunia.Git.Repositories.Models;
using Xunit;

namespace Gitunia.Core.Tests.DiffPane;

public class DiffPaneReducerTests
{
    private const int WideWidth = 100;
    private const int NarrowWidth = 60;
    private const int TallHeight = 40;
    private const int ShortHeight = 10;

    private static readonly ObjectId First = Id(1);
    private static readonly ObjectId Second = Id(2);

    [Fact]
    public void GivenANarrowScreen_WhenOpeningAFile_ThenThePaneOpensLoadsTheCommitAndTakesFocus()
    {
        var state = WithTab(NarrowWidth, TallHeight, BottomTab.Changes, PaneKind.Detail);

        var update = OpenFile(state, FileCursor("b.txt"));

        var diff = DiffOf(update);
        Assert.True(diff.IsOpen);
        Assert.Equal("b.txt", diff.PendingFile);
        Assert.Equal(PaneKind.Diff, ViewOf(update).FocusedPane);
        var load = Assert.IsType<LoadDiffEffect>(Assert.Single(update.Effects));
        Assert.Equal(new DiffTarget(First, null, false, false), load.Target);
        Assert.False(load.Prefetch);
    }

    [Fact]
    public void GivenAWideScreen_WhenOpeningAFile_ThenThePaneStaysPartOfTheTabAndTakesFocus()
    {
        var state = WithTab(WideWidth, TallHeight, BottomTab.Changes, PaneKind.Detail);

        var update = OpenFile(state, FileCursor("b.txt"));

        Assert.False(DiffOf(update).IsOpen);
        Assert.Equal("b.txt", DiffOf(update).PendingFile);
        Assert.Equal(PaneKind.Diff, ViewOf(update).FocusedPane);
    }

    [Fact]
    public void GivenAFolderAndAFileUnderTheCursor_WhenOpening_ThenTheFolderWins()
    {
        var state = WithTab(NarrowWidth, TallHeight, BottomTab.Changes, PaneKind.Detail);

        var update = OpenFile(state, new DiffCursor("src", "src/a.txt"));

        Assert.Equal("src", DiffOf(update).PendingFolder);
        Assert.Null(DiffOf(update).PendingFile);
    }

    [Fact]
    public void GivenAScreenTooShortForTheStrip_WhenOpeningAFile_ThenThePaneOpensZoomed()
    {
        var state = WithTab(WideWidth, ShortHeight, BottomTab.Changes, PaneKind.Detail);

        var update = OpenFile(state, FileCursor("b.txt"));

        Assert.Equal(PaneKind.Diff, DiffOf(update).Zoomed);
    }

    [Fact]
    public void GivenAZoomedPairBesideTheList_WhenOpeningAFile_ThenTheZoomIsKept()
    {
        var state = WithTab(WideWidth, TallHeight, BottomTab.Changes, PaneKind.Detail, new DiffPaneState(Zoomed: PaneKind.Detail));

        var update = OpenFile(state, FileCursor("b.txt"));

        Assert.Equal(PaneKind.Detail, DiffOf(update).Zoomed);
    }

    [Fact]
    public void GivenDetailsStillLoading_WhenOpeningAFile_ThenNothingOpensAndANoticeSaysWhy()
    {
        var state = WithTab(NarrowWidth, TallHeight, BottomTab.Changes, PaneKind.Detail);

        var update = Reduce(state, new DiffFileOpened(TabOf(state), new DiffOpenFacts(null, true, FileCursor("a"))));

        Assert.Same(state, update.State);
        Assert.Equal(DiffPaneReducer.DetailsLoadingNotice, NoticeOf(update));
    }

    [Theory]
    [InlineData(false, "a.txt", "Open the Changes tab (gc) to pick a file")]
    [InlineData(true, null, "Select a file or folder to see its diff")]
    public void GivenNoFileToOpen_WhenOpening_ThenNothingOpensAndANoticeSaysWhy(
        bool isChangesTab,
        string? file,
        string notice)
    {
        var state = WithTab(NarrowWidth, TallHeight, BottomTab.Changes, PaneKind.Detail);
        var facts = new DiffOpenFacts(Target(), isChangesTab, new DiffCursor(null, file));

        var update = Reduce(state, new DiffFileOpened(TabOf(state), facts));

        Assert.Same(state, update.State);
        Assert.Equal(notice, NoticeOf(update));
    }

    [Fact]
    public void GivenAnOpenPaneOnAnotherCommit_WhenFollowing_ThenTheCommitLoadsWithItsNeighboursAndTheFileIsKept()
    {
        var state = WithTab(NarrowWidth, TallHeight, BottomTab.Changes, PaneKind.Diff, new DiffPaneState(IsOpen: true));

        var update = Follow(state, Facts(storeLoaded: true, currentFile: "b.txt"));

        Assert.Equal("b.txt", DiffOf(update).PendingFile);
        var load = Assert.IsType<LoadDiffEffect>(Assert.Single(update.Effects));
        Assert.True(load.Prefetch);
    }

    [Fact]
    public void GivenAComparison_WhenFollowing_ThenTheNeighboursAreNotPrefetched()
    {
        var state = WithTab(NarrowWidth, TallHeight, BottomTab.Changes, PaneKind.Diff, new DiffPaneState(IsOpen: true));
        var facts = Facts() with { Target = new DiffTarget(First, Second, true, true) };

        var update = Follow(state, facts);

        Assert.False(Assert.IsType<LoadDiffEffect>(Assert.Single(update.Effects)).Prefetch);
    }

    [Fact]
    public void GivenAFolderShown_WhenFollowing_ThenTheFolderIsShownAgainWhateverTheStoreHolds()
    {
        var state = WithTab(NarrowWidth, TallHeight, BottomTab.Changes, PaneKind.Diff, new DiffPaneState(IsOpen: true, ShownFolder: "src"));

        var update = Follow(state, Facts(storeLoaded: true, currentFile: "src/a.txt"));

        Assert.Equal("src", DiffOf(update).PendingFolder);
        Assert.Null(DiffOf(update).PendingFile);
    }

    [Fact]
    public void GivenTheStoreStillLoading_WhenFollowing_ThenTheRememberedFileIsLeftAlone()
    {
        var state = WithTab(NarrowWidth, TallHeight, BottomTab.Changes, PaneKind.Diff, new DiffPaneState(IsOpen: true, PendingFile: "kept.txt"));

        var update = Follow(state, Facts(storeLoaded: false, currentFile: null));

        Assert.Equal("kept.txt", DiffOf(update).PendingFile);
    }

    [Fact]
    public void GivenTheDiffBesideTheList_WhenFollowing_ThenTheFileUnderTheCursorIsWanted()
    {
        var state = WithTab(WideWidth, TallHeight, BottomTab.Changes, PaneKind.Detail);

        var update = Follow(state, Facts() with { Cursor = FileCursor("c.txt") });

        Assert.Equal("c.txt", DiffOf(update).PendingFile);
        Assert.IsType<LoadDiffEffect>(Assert.Single(update.Effects));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void GivenNothingToLoad_WhenFollowing_ThenNothingChanges(bool storeShowsTarget, bool hasNoTarget)
    {
        var state = WithTab(WideWidth, TallHeight, BottomTab.Changes, PaneKind.Detail);
        var facts = Facts() with { StoreShowsTarget = storeShowsTarget };
        if (hasNoTarget)
        {
            facts = facts with { Target = null };
        }

        var update = Follow(state, facts);

        Assert.Same(state, update.State);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenAClosedPaneOnANarrowScreen_WhenFollowing_ThenNothingLoads()
    {
        var state = WithTab(NarrowWidth, TallHeight, BottomTab.Changes, PaneKind.Detail);

        var update = Follow(state, Facts());

        Assert.Same(state, update.State);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenAFileShownForAnotherCommit_WhenFollowing_ThenTheContentCloses()
    {
        var state = WithTab(NarrowWidth, TallHeight, BottomTab.Files, PaneKind.Diff, new DiffPaneState(IsOpen: true, ContentPath: "a.txt", ContentCommit: First));

        var update = Follow(state, Facts() with { GraphCommit = Second });

        Assert.Equal(new DiffPaneState(), DiffOf(update));
        Assert.IsType<ClearFileContentEffect>(Assert.Single(update.Effects));
        Assert.Equal(PaneKind.Detail, ViewOf(update).FocusedPane);
    }

    [Fact]
    public void GivenAFileShownForTheSelectedCommit_WhenFollowing_ThenTheContentStays()
    {
        var state = WithTab(NarrowWidth, TallHeight, BottomTab.Files, PaneKind.Diff, new DiffPaneState(IsOpen: true, ContentPath: "a.txt", ContentCommit: First));

        var update = Follow(state, Facts());

        Assert.Same(state, update.State);
    }

    [Fact]
    public void GivenTheFilesTabBesideTheTree_WhenAFileIsUnderTheCursor_ThenItsContentLoadsAfterTheFollowDelay()
    {
        var state = WithTab(WideWidth, TallHeight, BottomTab.Files, PaneKind.Detail);
        var row = FileRow("a.txt");

        var update = Follow(state, Facts() with { Content = DiffContentTarget.Of(row, First, First) });

        Assert.Equal("a.txt", DiffOf(update).ContentPath);
        Assert.Equal(First, DiffOf(update).ContentCommit);
        Assert.False(DiffOf(update).IsOpen);
        var load = Assert.IsType<LoadFileContentEffect>(Assert.Single(update.Effects));
        Assert.Equal(DiffPaneReducer.ContentFollowDelay, load.Delay);
        Assert.Equal(row, load.Row);
    }

    [Fact]
    public void GivenAShownFile_WhenTheCursorMovesToTheSameFile_ThenNothingIsLoadedAgain()
    {
        var state = WithTab(WideWidth, TallHeight, BottomTab.Files, PaneKind.Detail, new DiffPaneState(ContentPath: "a.txt", ContentCommit: First));

        var update = Reduce(
            state,
            new DiffCursorMoved(TabOf(state), DiffCursor.None, DiffContentTarget.Of(FileRow("a.txt"), First, First)));

        Assert.Same(state, update.State);
        Assert.Empty(update.Effects);
    }

    [Theory]
    [InlineData(FileTreeRowKind.Directory, DiffPlaceholder.File)]
    [InlineData(FileTreeRowKind.Submodule, DiffPlaceholder.Submodule)]
    public void GivenAShownFile_WhenTheCursorMovesToARowWithoutContent_ThenThePlaceholderReplacesIt(
        FileTreeRowKind kind,
        DiffPlaceholder placeholder)
    {
        var state = WithTab(WideWidth, TallHeight, BottomTab.Files, PaneKind.Detail, new DiffPaneState(ContentPath: "a.txt", ContentCommit: First));
        var row = FileRow("docs") with { Kind = kind };

        var update = Reduce(
            state,
            new DiffCursorMoved(TabOf(state), DiffCursor.None, DiffContentTarget.Of(row, First, First)));

        Assert.Equal(placeholder, DiffOf(update).Placeholder);
        Assert.Null(DiffOf(update).ContentPath);
        Assert.IsType<ClearFileContentEffect>(Assert.Single(update.Effects));
    }

    [Fact]
    public void GivenAFileOfAnotherCommitsTree_WhenTheCursorIsOnIt_ThenThePlaceholderShows()
    {
        var target = DiffContentTarget.Of(FileRow("a.txt"), First, Second);

        Assert.Null(target.File);
        Assert.Equal(DiffPlaceholder.File, target.Placeholder);
    }

    [Fact]
    public void GivenTheChangesTabBesideTheList_WhenTheCursorMovesToAFile_ThenThatFileIsWanted()
    {
        var state = WithTab(WideWidth, TallHeight, BottomTab.Changes, PaneKind.Detail);

        var update = Reduce(
            state,
            new DiffCursorMoved(TabOf(state), FileCursor("b.txt"), DiffContentTarget.None));

        Assert.Equal("b.txt", DiffOf(update).PendingFile);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenANarrowScreen_WhenTheCursorMoves_ThenTheClosedPaneIsLeftAlone()
    {
        var state = WithTab(NarrowWidth, TallHeight, BottomTab.Changes, PaneKind.Detail);

        var update = Reduce(state, new DiffCursorMoved(TabOf(state), FileCursor("b.txt"), DiffContentTarget.None));

        Assert.Same(state, update.State);
    }

    [Fact]
    public void GivenAFileRow_WhenOpeningItsContentOnANarrowScreen_ThenThePaneOpensAndTakesFocus()
    {
        var state = WithTab(NarrowWidth, TallHeight, BottomTab.Files, PaneKind.Detail);
        var row = FileRow("a.txt");

        var update = Reduce(state, new DiffContentOpened(TabOf(state), row, First));

        Assert.Equal(new DiffPaneState(IsOpen: true, ContentPath: "a.txt", ContentCommit: First), DiffOf(update));
        Assert.Equal(PaneKind.Diff, ViewOf(update).FocusedPane);
        var load = Assert.IsType<LoadFileContentEffect>(Assert.Single(update.Effects));
        Assert.Equal(TimeSpan.Zero, load.Delay);
    }

    [Fact]
    public void GivenAFileRow_WhenOpeningItsContentBesideTheTree_ThenThePaneIsNotOpenedOnDemand()
    {
        var state = WithTab(WideWidth, TallHeight, BottomTab.Files, PaneKind.Detail);

        var update = Reduce(state, new DiffContentOpened(TabOf(state), FileRow("a.txt"), First));

        Assert.False(DiffOf(update).IsOpen);
        Assert.Equal(PaneKind.Diff, ViewOf(update).FocusedPane);
    }

    [Fact]
    public void GivenTheSameFileAlreadyOpen_WhenOpeningItAgain_ThenOnlyFocusMovesAndNothingReloads()
    {
        var state = WithTab(NarrowWidth, TallHeight, BottomTab.Files, PaneKind.Detail, new DiffPaneState(IsOpen: true, ContentPath: "a.txt", ContentCommit: First));

        var update = Reduce(state, new DiffContentOpened(TabOf(state), FileRow("a.txt"), First));

        Assert.Equal(DiffOf(state), DiffOf(update));
        Assert.Empty(update.Effects);
        Assert.Equal(PaneKind.Diff, ViewOf(update).FocusedPane);
    }

    [Fact]
    public void GivenATreeStillLoading_WhenOpeningAFile_ThenANoticeSaysSo()
    {
        var state = WithTab(NarrowWidth, TallHeight, BottomTab.Files, PaneKind.Detail);

        var update = Reduce(state, new DiffContentOpened(TabOf(state), FileRow("a.txt"), null));

        Assert.Same(state, update.State);
        Assert.Equal(DiffPaneReducer.TreeLoadingNotice, NoticeOf(update));
    }

    [Fact]
    public void GivenASubmoduleRow_WhenOpeningItsContent_ThenANoticeSaysThereIsNone()
    {
        var state = WithTab(NarrowWidth, TallHeight, BottomTab.Files, PaneKind.Detail);
        var row = FileRow("mod") with { Kind = FileTreeRowKind.Submodule, Blob = null };

        var update = Reduce(state, new DiffContentOpened(TabOf(state), row, First));

        Assert.Same(state, update.State);
        Assert.Equal(DiffPaneReducer.SubmoduleNotice, NoticeOf(update));
    }

    [Fact]
    public void GivenAPendingFileAndALoadedDiff_WhenRefreshed_ThenTheFileIsTakenAndRemembered()
    {
        var state = WithTab(WideWidth, TallHeight, BottomTab.Changes, PaneKind.Detail, new DiffPaneState(PendingFile: "b.txt", ShownFolder: "src"));

        var update = Reduce(state, new DiffRefreshed(TabOf(state), Document("a.txt", "b.txt"), true));

        var diff = DiffOf(update);
        Assert.Null(diff.PendingFile);
        Assert.Null(diff.ShownFolder);
        Assert.Equal("b.txt", diff.ShownPath);
    }

    [Fact]
    public void GivenAPendingFileAndALoadingDiff_WhenRefreshed_ThenTheWishWaitsForTheLoad()
    {
        var state = WithTab(WideWidth, TallHeight, BottomTab.Changes, PaneKind.Detail, new DiffPaneState(PendingFile: "b.txt", ShownPath: "a.txt"));

        var update = Reduce(state, new DiffRefreshed(TabOf(state), DiffDocument.Empty, false));

        Assert.Equal("b.txt", DiffOf(update).PendingFile);
        Assert.Null(DiffOf(update).ShownPath);
    }

    [Fact]
    public void GivenAPendingFolderWithChanges_WhenRefreshed_ThenTheFolderIsShown()
    {
        var state = WithTab(WideWidth, TallHeight, BottomTab.Changes, PaneKind.Detail, new DiffPaneState(PendingFolder: "src"));

        var update = Reduce(state, new DiffRefreshed(TabOf(state), Document("top.txt", "src/a.txt", "src/b.txt"), true));

        Assert.Equal("src", DiffOf(update).ShownFolder);
        Assert.Null(DiffOf(update).PendingFolder);
        Assert.Equal("src/a.txt", DiffOf(update).ShownPath);
    }

    [Fact]
    public void GivenAFolderWithoutChanges_WhenRefreshed_ThenThePaneFallsBackToASingleFile()
    {
        var state = WithTab(WideWidth, TallHeight, BottomTab.Changes, PaneKind.Detail, new DiffPaneState(PendingFolder: "docs"));

        var update = Reduce(state, new DiffRefreshed(TabOf(state), Document("a.txt"), true));

        Assert.Null(DiffOf(update).ShownFolder);
        Assert.Equal("a.txt", DiffOf(update).ShownPath);
    }

    [Fact]
    public void GivenAnOptionKey_WhenItChangesTheOptions_ThenTheDiffLoadsAgainAtTheSameFileAndTheNoticeDescribesThem()
    {
        var state = OpenPane();
        var current = DiffOptions.Default;

        var update = Reduce(
            state,
            new DiffOptionsRequested(TabOf(state), ActionIds.DiffToggleIgnoreWhitespace, current, "b.txt"));

        Assert.Equal("b.txt", DiffOf(update).PendingFile);
        var effect = Assert.IsType<SetDiffOptionsEffect>(update.Effects[0]);
        Assert.Equal(current with { IgnoreWhitespace = true }, effect.Options);
        Assert.Equal(
            $"Diff: {current.ContextLines.ToString(CultureInfo.InvariantCulture)} context lines, whitespace ignored",
            Assert.IsType<PostStatusEffect>(update.Effects[1]).Text);
    }

    [Fact]
    public void GivenNoContextLeft_WhenAskingForLess_ThenNothingReloadsAndANoticeSaysSo()
    {
        var state = OpenPane();
        var none = DiffOptions.Default with { ContextLines = 0 };

        var update = Reduce(state, new DiffOptionsRequested(TabOf(state), ActionIds.DiffLessContext, none, "b.txt"));

        Assert.Same(state, update.State);
        Assert.Equal(DiffPaneReducer.ContextLimitNotice, NoticeOf(update));
    }

    [Fact]
    public void GivenAKeyThatIsNoOptionKey_WhenAskedAsOne_ThenNothingHappens()
    {
        var state = OpenPane();

        var update = Reduce(
            state,
            new DiffOptionsRequested(TabOf(state), ActionIds.DiffNextHunk, DiffOptions.Default, null));

        Assert.Same(state, update.State);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenAFolderShown_WhenChangingTheOptions_ThenTheFolderStaysShown()
    {
        var state = OpenPane(diff: new DiffPaneState(IsOpen: true, ShownFolder: "src"));

        var update = Reduce(
            state,
            new DiffOptionsRequested(TabOf(state), ActionIds.DiffMoreContext, DiffOptions.Default, "src/a.txt"));

        Assert.Equal("src", DiffOf(update).PendingFolder);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GivenFileContentShown_WhenAnOptionOrLoadAnywayKeyArrives_ThenANoticeSaysThereAreNoOptions(
        bool loadAnyway)
    {
        var state = OpenPane(
            BottomTab.Files,
            new DiffPaneState(IsOpen: true, ContentPath: "a.txt", ContentCommit: First));
        DiffPaneCommand command = loadAnyway
            ? new DiffLoadAnywayRequested(TabOf(state), true, null)
            : new DiffOptionsRequested(TabOf(state), ActionIds.DiffMoreContext, DiffOptions.Default, null);

        var update = Reduce(state, command);

        Assert.Same(state, update.State);
        Assert.Equal(DiffPaneReducer.ContentHasNoOptionsNotice, NoticeOf(update));
    }

    [Fact]
    public void GivenACutDiff_WhenLoadingAnyway_ThenTheWholeDiffLoadsAtTheSameFile()
    {
        var state = OpenPane();

        var update = Reduce(state, new DiffLoadAnywayRequested(TabOf(state), true, "b.txt"));

        Assert.Equal("b.txt", DiffOf(update).PendingFile);
        Assert.IsType<LoadWholeDiffEffect>(update.Effects[0]);
        Assert.Equal(DiffPaneReducer.LoadingWholeNotice, Assert.IsType<PostStatusEffect>(update.Effects[1]).Text);
    }

    [Fact]
    public void GivenAnUncutDiff_WhenLoadingAnyway_ThenANoticeSaysNothingWasCut()
    {
        var state = OpenPane();

        var update = Reduce(state, new DiffLoadAnywayRequested(TabOf(state), false, "b.txt"));

        Assert.Same(state, update.State);
        Assert.Equal(DiffPaneReducer.NothingCutNotice, NoticeOf(update));
    }

    [Fact]
    public void GivenAFileChosenWithTheFileKeys_WhenReduced_ThenItIsPendingAndAFolderWishIsDropped()
    {
        var state = OpenPane(diff: new DiffPaneState(IsOpen: true, PendingFolder: "src"));

        var update = Reduce(state, new DiffFileChosen(TabOf(state), "b.txt"));

        Assert.Equal("b.txt", DiffOf(update).PendingFile);
        Assert.Null(DiffOf(update).PendingFolder);
    }

    [Fact]
    public void GivenAPaneOpenedOnDemand_WhenClosing_ThenEverythingIsForgottenAndTheFileListHasFocus()
    {
        var state = WithTab(NarrowWidth, TallHeight, BottomTab.Changes, PaneKind.Diff, new DiffPaneState(IsOpen: true, Zoomed: PaneKind.Diff, ShownFolder: "src", ShownPath: "src/a.txt"));

        var update = Reduce(state, new DiffClosed(TabOf(state)));

        Assert.Equal(DiffPaneState.Empty, DiffOf(update));
        Assert.IsType<ClearDiffEffect>(Assert.Single(update.Effects));
        Assert.Equal(PaneKind.Detail, ViewOf(update).FocusedPane);
    }

    [Fact]
    public void GivenThePaneBesideTheList_WhenClosing_ThenTheDiffStaysLoadedAndFocusReturnsToTheList()
    {
        var state = WithTab(WideWidth, TallHeight, BottomTab.Changes, PaneKind.Diff, new DiffPaneState(IsOpen: true, ShownPath: "b.txt"));

        var update = Reduce(state, new DiffClosed(TabOf(state)));

        Assert.Equal(new DiffPaneState(ShownPath: "b.txt"), DiffOf(update));
        Assert.Empty(update.Effects);
        Assert.Equal(PaneKind.Detail, ViewOf(update).FocusedPane);
    }

    [Fact]
    public void GivenTheFocusedPane_WhenZooming_ThenItIsZoomedAndZoomingAgainUnzoomsIt()
    {
        var state = WithTab(NarrowWidth, TallHeight, BottomTab.Changes, PaneKind.Graph);

        var zoomed = Reduce(state, new DiffZoomed(TabOf(state)));
        var unzoomed = Reduce(zoomed.State, new DiffZoomed(TabOf(state)));

        Assert.Equal(PaneKind.Graph, DiffOf(zoomed).Zoomed);
        Assert.Null(DiffOf(unzoomed).Zoomed);
    }

    [Fact]
    public void GivenTheSidebarFocused_WhenZooming_ThenANoticeSaysItCannotBeZoomed()
    {
        var state = WithTab(NarrowWidth, TallHeight, BottomTab.Changes, PaneKind.Sidebar);

        var update = Reduce(state, new DiffZoomed(TabOf(state)));

        Assert.Same(state, update.State);
        Assert.Equal(DiffPaneReducer.SidebarZoomNotice, NoticeOf(update));
    }

    [Fact]
    public void GivenADetailZoomBesideTheDiff_WhenFocusMovesToTheDiff_ThenTheZoomIsKeptTogether()
    {
        var state = WithTab(WideWidth, TallHeight, BottomTab.Changes, PaneKind.Detail, new DiffPaneState(Zoomed: PaneKind.Detail));

        var update = AppReducer.Reduce(state, new TabCommand(TabOf(state), new FocusPaneCommand(PaneKind.Diff)));

        Assert.Equal(PaneKind.Detail, DiffOf(update).Zoomed);
    }

    [Fact]
    public void GivenAZoomedPane_WhenFocusMovesToAPaneOutsideThePair_ThenTheZoomIsForgotten()
    {
        var state = WithTab(WideWidth, TallHeight, BottomTab.Changes, PaneKind.Graph, new DiffPaneState(Zoomed: PaneKind.Graph));

        var update = AppReducer.Reduce(state, new TabCommand(TabOf(state), new FocusPaneCommand(PaneKind.Detail)));

        Assert.Null(DiffOf(update).Zoomed);
    }

    [Fact]
    public void GivenAZoomThatDoesNotMatchTheFocus_WhenAskingForTheEffectiveZoom_ThenItIsHidden()
    {
        var diff = new DiffPaneState(Zoomed: PaneKind.Graph);

        Assert.Null(DiffPaneRules.EffectiveZoom(diff, PaneKind.Diff, true));
        Assert.Equal(PaneKind.Graph, DiffPaneRules.EffectiveZoom(diff, PaneKind.Graph, true));
        Assert.Equal(PaneKind.Diff, DiffPaneRules.EffectiveZoom(new DiffPaneState(Zoomed: PaneKind.Detail), PaneKind.Diff, true));
        Assert.Null(DiffPaneRules.EffectiveZoom(new DiffPaneState(Zoomed: PaneKind.Detail), PaneKind.Diff, false));
    }

    [Fact]
    public void GivenFileContentOnTheFilesTab_WhenAnotherTabIsShown_ThenTheContentIsDropped()
    {
        var state = WithTab(WideWidth, TallHeight, BottomTab.Files, PaneKind.Detail, new DiffPaneState(ContentPath: "a.txt", ContentCommit: First));

        var update = AppReducer.Reduce(state, new TabCommand(TabOf(state), new SetBottomTabCommand(BottomTab.Changes)));

        Assert.Null(DiffOf(update).ContentPath);
        Assert.IsType<ClearFileContentEffect>(Assert.Single(update.Effects));
    }

    [Fact]
    public void GivenThePlaceholderOnTheFilesTab_WhenAnotherTabIsShown_ThenThePlaceholderGoes()
    {
        var state = WithTab(WideWidth, TallHeight, BottomTab.Files, PaneKind.Detail, new DiffPaneState(Placeholder: DiffPlaceholder.File));

        var update = AppReducer.Reduce(state, new TabCommand(TabOf(state), new SetBottomTabCommand(BottomTab.Details)));

        Assert.Equal(DiffPlaceholder.None, DiffOf(update).Placeholder);
    }

    [Fact]
    public void GivenTheDiffHasFocus_WhenTheScreenShrinksBelowSideBySide_ThenFocusReturnsToTheFileList()
    {
        var state = WithTab(WideWidth, TallHeight, BottomTab.Changes, PaneKind.Diff);

        var update = AppReducer.Reduce(state, Resize(NarrowWidth, TallHeight));

        Assert.Equal(PaneKind.Detail, ViewOf(update).FocusedPane);
    }

    [Fact]
    public void GivenAPaneOpenedOnDemand_WhenTheScreenShrinks_ThenItKeepsFocus()
    {
        var state = WithTab(NarrowWidth, TallHeight, BottomTab.Changes, PaneKind.Diff, new DiffPaneState(IsOpen: true));

        var update = AppReducer.Reduce(state, Resize(NarrowWidth - 1, TallHeight));

        Assert.Equal(PaneKind.Diff, ViewOf(update).FocusedPane);
    }

    [Fact]
    public void GivenAnUnknownScreenSize_WhenAnyCommandIsReduced_ThenTheFocusOnTheDiffIsLeftAlone()
    {
        var state = Open(AppState.Empty, "first");
        state = AppReducer.Reduce(state, new TabCommand(TabOf(state), new FocusPaneCommand(PaneKind.Diff))).State;

        var update = AppReducer.Reduce(state, new TabCommand(TabOf(state), new SelectCommitCommand(null)));

        Assert.Equal(PaneKind.Diff, ViewOf(update).FocusedPane);
    }

    [Fact]
    public void GivenAPaneWithState_WhenTheTabsStoresAreRebuilt_ThenThePaneStartsOver()
    {
        var state = WithTab(NarrowWidth, TallHeight, BottomTab.Changes, PaneKind.Detail, new DiffPaneState(IsOpen: true, ShownPath: "a.txt"));

        var update = Reduce(state, new DiffPaneReset(TabOf(state)));

        Assert.Equal(DiffPaneState.Empty, DiffOf(update));
    }

    [Fact]
    public void GivenATabThatWasClosed_WhenItsCommandArrives_ThenItIsDropped()
    {
        var state = WithTab(NarrowWidth, TallHeight, BottomTab.Changes, PaneKind.Detail);
        var closed = AppReducer.Reduce(state, new CloseTabCommand(TabOf(state))).State;

        var update = Reduce(closed, new DiffZoomed(TabOf(state)));

        Assert.Same(closed, update.State);
        Assert.Empty(update.Effects);
    }

    private static AppUpdate Reduce(AppState state, DiffPaneCommand command) => AppReducer.Reduce(state, command);

    private static AppUpdate OpenFile(AppState state, DiffCursor cursor) =>
        Reduce(state, new DiffFileOpened(TabOf(state), new DiffOpenFacts(Target(), true, cursor)));

    private static AppUpdate Follow(AppState state, DiffFollowFacts facts) =>
        Reduce(state, new DiffFollowed(TabOf(state), facts));

    private static DiffFollowFacts Facts(bool storeLoaded = false, string? currentFile = null) => new(
        First,
        Target(),
        false,
        storeLoaded,
        currentFile,
        DiffCursor.None,
        DiffContentTarget.None);

    private static DiffTarget Target() => new(First, null, false, false);

    private static DiffCursor FileCursor(string path) => new(null, path);

    private static FileTreeRow FileRow(string path) =>
        new(FileTreeRowKind.File, path, path, 0, false, 3, 0, Id(7));

    private static DiffDocument Document(params string[] paths)
    {
        var builder = new DiffDocumentBuilder();
        foreach (var path in paths)
        {
            builder.Add(DiffParser.Parse(Patch(path)).Items[0]);
        }

        return builder.Build([]);
    }

    private static string Patch(string path) =>
        $"diff --git a/{path} b/{path}\n--- a/{path}\n+++ b/{path}\n@@ -1 +1 @@\n-old\n+new\n";

    private static string NoticeOf(AppUpdate update) =>
        Assert.IsType<PostStatusEffect>(Assert.Single(update.Effects)).Text;

    private static DiffPaneState DiffOf(AppUpdate update) => DiffOf(update.State);

    private static DiffPaneState DiffOf(AppState state) => state.Workspace.ActiveTab!.View.DiffPane;

    private static RepositoryViewState ViewOf(AppUpdate update) => update.State.Workspace.ActiveTab!.View;

    private static TabId TabOf(AppState state) => state.Workspace.ActiveTabId!.Value;

    private static ObjectId Id(int number)
    {
        Assert.True(ObjectId.TryParse(number.ToString("x40", CultureInfo.InvariantCulture), out var id));

        return id;
    }

    private static ScreenResizedCommand Resize(int width, int height) =>
        new(new ScreenExtent(width, height), DateTimeOffset.UnixEpoch);

    private static AppState OpenPane(BottomTab bottomTab = BottomTab.Changes, DiffPaneState? diff = null) =>
        WithTab(NarrowWidth, TallHeight, bottomTab, PaneKind.Diff, diff ?? new DiffPaneState(IsOpen: true));

    private static AppState Open(AppState state, string name)
    {
        var root = Path.Combine(Path.GetTempPath(), $"gitunia-diff-pane-reducer-{name}");
        var gitDirectory = Path.Combine(root, ".git");
        var location = new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);

        return AppReducer.Reduce(state, new OpenRepositoryCommand(location)).State;
    }

    // A tab on a screen of the given size, showing a bottom tab, with the given pane focused.
    private static AppState WithTab(
        int width,
        int height,
        BottomTab bottomTab,
        PaneKind focus,
        DiffPaneState? diff = null)
    {
        var state = Open(AppState.Empty, "first");
        var tab = TabOf(state);
        state = AppReducer.Reduce(state, Resize(width, height)).State;
        state = AppReducer.Reduce(state, new TabCommand(tab, new SetBottomTabCommand(bottomTab))).State;
        state = AppReducer.Reduce(state, new TabCommand(tab, new FocusPaneCommand(focus))).State;
        if (diff is not null)
        {
            // The pane state is set with the focus in place, so the rules that follow every command have nothing to undo.
            state = AppReducer.Reduce(state, new TabCommand(tab, new SetDiffPaneCommand(diff))).State;
            state = AppReducer.Reduce(state, new TabCommand(tab, new FocusPaneCommand(focus))).State;
        }

        return state;
    }
}
