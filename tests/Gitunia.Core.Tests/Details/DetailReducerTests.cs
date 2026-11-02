using Gitunia.Core.Details;
using Gitunia.Core.Effects;
using Gitunia.Core.FileTree;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Keymap;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Repositories.Models;
using Xunit;

namespace Gitunia.Core.Tests.Details;

public class DetailReducerTests
{
    private static readonly FileTreeRow ClosedDirectory = Row(FileTreeRowKind.Directory, "src", false);
    private static readonly FileTreeRow OpenDirectory = Row(FileTreeRowKind.Directory, "docs", true);
    private static readonly FileTreeRow File = Row(FileTreeRowKind.File, "README", false);

    [Theory]
    [InlineData(BottomTab.Details, DetailTabMove.Next, BottomTab.Changes)]
    [InlineData(BottomTab.Changes, DetailTabMove.Next, BottomTab.Files)]
    [InlineData(BottomTab.Files, DetailTabMove.Next, BottomTab.Details)]
    [InlineData(BottomTab.Details, DetailTabMove.Previous, BottomTab.Files)]
    [InlineData(BottomTab.Files, DetailTabMove.Previous, BottomTab.Changes)]
    [InlineData(BottomTab.Changes, DetailTabMove.Previous, BottomTab.Details)]
    [InlineData(BottomTab.Changes, DetailTabMove.Details, BottomTab.Details)]
    [InlineData(BottomTab.Details, DetailTabMove.Changes, BottomTab.Changes)]
    [InlineData(BottomTab.Details, DetailTabMove.Files, BottomTab.Files)]
    [InlineData(BottomTab.Files, DetailTabMove.Files, BottomTab.Files)]
    public void GivenATabMove_WhenReducing_ThenTheTabsBottomTabIsTheTarget(BottomTab start,
        DetailTabMove move,
        BottomTab expected)
    {
        var state = WithTab(start);

        var update = DetailReducer.Reduce(state, new DetailTabMoved(Tab(state), move));

        Assert.Equal(expected, ActiveView(update).BottomTab);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenTheTabAlreadyShowing_WhenSelectingItAgain_ThenTheStateIsTheSame()
    {
        var state = WithTab(BottomTab.Files);

        var update = DetailReducer.Reduce(state, new DetailTabMoved(Tab(state), DetailTabMove.Files));

        Assert.Same(state, update.State);
    }

    [Fact]
    public void GivenTwoTabs_WhenSwitchingOne_ThenTheOtherKeepsItsBottomTab()
    {
        var state = WithTab(BottomTab.Changes);
        var first = Tab(state);
        state = Open(state, "second");

        var update = DetailReducer.Reduce(state, new DetailTabMoved(Tab(state), DetailTabMove.Details));

        Assert.Equal(BottomTab.Details, ActiveView(update).BottomTab);
        Assert.Equal(BottomTab.Changes, update.State.Workspace.FindTab(first)!.View.BottomTab);
    }

    [Fact]
    public void GivenAClosedTab_WhenACommandArrives_ThenNothingChanges()
    {
        var state = WithTab(BottomTab.Changes);

        var update = DetailReducer.Reduce(state, new DetailTabMoved(new TabId(99), DetailTabMove.Files));

        Assert.Same(state, update.State);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenTheToggle_WhenReducing_ThenThePathStyleStepsAndTheNoticeSaysWhich()
    {
        var state = WithTab(BottomTab.Changes);
        var tab = Tab(state);

        var first = DetailReducer.Reduce(state, new PathStyleChosen(tab, null));
        var second = DetailReducer.Reduce(first.State, new PathStyleChosen(tab, null));
        var third = DetailReducer.Reduce(second.State, new PathStyleChosen(tab, null));

        Assert.Equal(FilePathStyle.FullPath, ActiveView(first).PathStyle);
        Assert.Equal("File paths: full path", NoticeOf(first));
        Assert.Equal(FilePathStyle.Tree, ActiveView(second).PathStyle);
        Assert.Equal("File paths: tree", NoticeOf(second));
        Assert.Equal(FilePathStyle.NameFirst, ActiveView(third).PathStyle);
        Assert.Equal("File paths: name first", NoticeOf(third));
    }

    [Theory]
    [InlineData(FilePathStyle.NameFirst, "File paths: name first")]
    [InlineData(FilePathStyle.FullPath, "File paths: full path")]
    [InlineData(FilePathStyle.Tree, "File paths: tree")]
    public void GivenAStyle_WhenChoosingItWhateverTheCurrentOne_ThenThatStyleIsSet(FilePathStyle style, string notice)
    {
        var state = WithTab(BottomTab.Changes);
        var toggled = DetailReducer.Reduce(state, new PathStyleChosen(Tab(state), null)).State;

        var update = DetailReducer.Reduce(toggled, new PathStyleChosen(Tab(toggled), style));

        Assert.Equal(style, ActiveView(update).PathStyle);
        Assert.Equal(notice, NoticeOf(update));
    }

    [Fact]
    public void GivenTheWorkingTreeView_WhenTogglingThePathStyle_ThenTheTreeStyleIsPartOfTheCycle()
    {
        var state = WithTab(BottomTab.Changes);
        var tab = Tab(state);
        var working = AppReducer.Reduce(state, new TabCommand(tab, new OpenWorkingTreeCommand())).State;

        var once = DetailReducer.Reduce(working, new PathStyleChosen(tab, null));
        var twice = DetailReducer.Reduce(once.State, new PathStyleChosen(tab, null));

        Assert.Equal(FilePathStyle.Tree, ActiveView(twice).PathStyle);
        Assert.Equal("File paths: tree", NoticeOf(twice));
    }

    [Fact]
    public void GivenTheFilesTab_WhenStartingTheFilter_ThenTheBoxOpensAndInsertModeStarts()
    {
        var state = WithTab(BottomTab.Files);

        var update = DetailReducer.Reduce(state, new FileFilterStarted(Tab(state)));

        Assert.True(ActiveView(update).DetailTree.IsEditingFilter);
        Assert.Equal(KeyMode.Insert, update.State.Shell.Mode);
    }

    [Theory]
    [InlineData(BottomTab.Details)]
    [InlineData(BottomTab.Changes)]
    public void GivenAnotherTab_WhenStartingTheFilter_ThenItOnlyExplains(BottomTab tab)
    {
        var state = WithTab(tab);

        var update = DetailReducer.Reduce(state, new FileFilterStarted(Tab(state)));

        Assert.False(ActiveView(update).DetailTree.IsEditingFilter);
        Assert.Equal(KeyMode.Normal, update.State.Shell.Mode);
        Assert.Equal(DetailReducer.WrongTabNotice, NoticeOf(update));
    }

    [Fact]
    public void GivenTheFilterBox_WhenTypingAndErasing_ThenTheFilterFollowsAndTheCursorGoesToTheTop()
    {
        var state = Editing();
        var tab = Tab(state);

        var typed = DetailReducer.Reduce(state, new FileFilterKeyTyped(tab, new KeyEvent("s")));
        var longer = DetailReducer.Reduce(typed.State, new FileFilterKeyTyped(tab, new KeyEvent("r")));
        var erased = DetailReducer.Reduce(longer.State, new FileFilterKeyTyped(tab, new KeyEvent(KeyNames.Backspace)));

        Assert.Equal("sr", ActiveView(longer).DetailTree.Filter);
        Assert.Equal("s", ActiveView(erased).DetailTree.Filter);
        Assert.Equal(0, Assert.IsType<MoveDetailCursorEffect>(Assert.Single(typed.Effects)).Row);
        Assert.True(ActiveView(erased).DetailTree.IsEditingFilter);
    }

    [Fact]
    public void GivenAKeyThatIsNoText_WhenTypingInTheBox_ThenNothingChanges()
    {
        var state = Editing();

        var update = DetailReducer.Reduce(state, new FileFilterKeyTyped(Tab(state), new KeyEvent("Enter")));

        Assert.Same(state, update.State);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenTheFilterBoxLeftOpenOnAnotherBottomTab_WhenTyping_ThenNothingChanges()
    {
        var state = Editing();
        state = AppReducer.Reduce(state, new TabCommand(Tab(state), new SetBottomTabCommand(BottomTab.Changes))).State;

        var update = DetailReducer.Reduce(state, new FileFilterKeyTyped(Tab(state), new KeyEvent("a")));

        Assert.Equal(string.Empty, ActiveView(update).DetailTree.Filter);
    }

    [Fact]
    public void GivenAFilter_WhenAccepting_ThenItStaysAndInsertModeEnds()
    {
        var state = Editing("src");

        var update = DetailReducer.Reduce(state, new FileFilterAccepted(Tab(state)));

        Assert.Equal("src", ActiveView(update).DetailTree.Filter);
        Assert.False(ActiveView(update).DetailTree.IsEditingFilter);
        Assert.Equal(KeyMode.Normal, update.State.Shell.Mode);
    }

    [Fact]
    public void GivenAFilterBeingTyped_WhenCancelling_ThenTheFilterIsClearedAndTheCursorGoesToTheTop()
    {
        var state = Editing("src");

        var update = DetailReducer.Reduce(state, new FileFilterCancelled(Tab(state)));

        Assert.Equal(string.Empty, ActiveView(update).DetailTree.Filter);
        Assert.False(ActiveView(update).DetailTree.IsEditingFilter);
        Assert.Equal(KeyMode.Normal, update.State.Shell.Mode);
        Assert.Equal(0, Assert.IsType<MoveDetailCursorEffect>(Assert.Single(update.Effects)).Row);
    }

    [Fact]
    public void GivenAKeptFilter_WhenClearingIt_ThenTheFilterGoesAndTheCursorGoesToTheTop()
    {
        var state = Editing("src");
        state = DetailReducer.Reduce(state, new FileFilterAccepted(Tab(state))).State;

        var update = DetailReducer.Reduce(state, new FileFilterCleared(Tab(state)));

        Assert.Equal(string.Empty, ActiveView(update).DetailTree.Filter);
        Assert.IsType<MoveDetailCursorEffect>(Assert.Single(update.Effects));
    }

    [Fact]
    public void GivenNoBoxOpen_WhenAcceptingOrCancelling_ThenNothingChanges()
    {
        var state = WithTab(BottomTab.Files);

        Assert.Same(state, DetailReducer.Reduce(state, new FileFilterAccepted(Tab(state))).State);
        Assert.Same(state, DetailReducer.Reduce(state, new FileFilterCancelled(Tab(state))).State);
    }

    [Fact]
    public void GivenAClosedDirectory_WhenOpening_ThenItIsAddedToTheOpenOnes()
    {
        var state = WithTab(BottomTab.Files);

        var update = DetailReducer.Reduce(state, new FileTreeOpened(Tab(state), new FileTreeCursor(ClosedDirectory, -1)));

        Assert.Equal(["src"], ActiveView(update).DetailTree.OpenDirectories);
    }

    [Fact]
    public void GivenAnOpenDirectory_WhenOpeningAgain_ThenItCloses()
    {
        var state = WithOpenDirectory("docs");

        var update = DetailReducer.Reduce(state, new FileTreeOpened(Tab(state), new FileTreeCursor(OpenDirectory, -1)));

        Assert.Empty(ActiveView(update).DetailTree.OpenDirectories);
    }

    [Fact]
    public void GivenAFileRow_WhenOpening_ThenTheFileIsHandedToTheContentView()
    {
        var state = WithTab(BottomTab.Files);

        var update = DetailReducer.Reduce(state, new FileTreeOpened(Tab(state), new FileTreeCursor(File, -1)));

        Assert.Equal(File, Assert.IsType<OpenFileContentEffect>(Assert.Single(update.Effects)).Row);
        Assert.Empty(ActiveView(update).DetailTree.OpenDirectories);
    }

    [Fact]
    public void GivenARowOutsideTheList_WhenOpening_ThenNothingHappens()
    {
        var state = WithTab(BottomTab.Files);

        var update = DetailReducer.Reduce(state, new FileTreeOpened(Tab(state), new FileTreeCursor(null, -1)));

        Assert.Same(state, update.State);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenAFilter_WhenOpeningADirectory_ThenItStaysAsItIs()
    {
        var state = Editing("src");

        var update = DetailReducer.Reduce(state, new FileTreeOpened(Tab(state), new FileTreeCursor(ClosedDirectory, -1)));

        Assert.Empty(ActiveView(update).DetailTree.OpenDirectories);
    }

    [Fact]
    public void GivenAClosedDirectory_WhenExpanding_ThenItOpensAndAFileIsNotOpened()
    {
        var state = WithTab(BottomTab.Files);
        var tab = Tab(state);

        var directory = DetailReducer.Reduce(state, new FileTreeExpanded(tab, new FileTreeCursor(ClosedDirectory, -1)));
        var file = DetailReducer.Reduce(state, new FileTreeExpanded(tab, new FileTreeCursor(File, -1)));

        Assert.Equal(["src"], ActiveView(directory).DetailTree.OpenDirectories);
        Assert.Same(state, file.State);
        Assert.Empty(file.Effects);
    }

    [Fact]
    public void GivenAnOpenDirectory_WhenCollapsing_ThenItClosesAndTheCursorStays()
    {
        var state = WithOpenDirectory("docs");

        var update = DetailReducer.Reduce(state, new FileTreeCollapsed(Tab(state), new FileTreeCursor(OpenDirectory, -1)));

        Assert.Empty(ActiveView(update).DetailTree.OpenDirectories);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenARowInsideADirectory_WhenCollapsing_ThenTheCursorGoesToTheParent()
    {
        var state = WithTab(BottomTab.Files);

        var update = DetailReducer.Reduce(state, new FileTreeCollapsed(Tab(state), new FileTreeCursor(File, 3)));

        Assert.Equal(3, Assert.IsType<MoveDetailCursorEffect>(Assert.Single(update.Effects)).Row);
        Assert.Same(state, update.State);
    }

    [Fact]
    public void GivenATopLevelRow_WhenCollapsing_ThenNothingMoves()
    {
        var state = WithTab(BottomTab.Files);

        var update = DetailReducer.Reduce(state, new FileTreeCollapsed(Tab(state), new FileTreeCursor(File, -1)));

        Assert.Same(state, update.State);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenAFilter_WhenCollapsingAnOpenDirectory_ThenTheCursorClimbsInsteadOfClosing()
    {
        var state = WithOpenDirectory("docs");
        state = DetailReducer.Reduce(state, new FileFilterStarted(Tab(state))).State;
        state = DetailReducer.Reduce(state, new FileFilterKeyTyped(Tab(state), new KeyEvent("d"))).State;

        var update = DetailReducer.Reduce(state, new FileTreeCollapsed(Tab(state), new FileTreeCursor(OpenDirectory, 0)));

        Assert.Equal(["docs"], ActiveView(update).DetailTree.OpenDirectories);
        Assert.Equal(0, Assert.IsType<MoveDetailCursorEffect>(Assert.Single(update.Effects)).Row);
    }

    [Fact]
    public void GivenAFolderOfTheChangesTree_WhenToggling_ThenItClosesAndOpensAgain()
    {
        var state = WithTab(BottomTab.Changes);
        var tab = Tab(state);
        var cursor = new ChangesCursor("src", -1);

        var closed = DetailReducer.Reduce(state, new ChangesFolderToggled(tab, cursor));
        var opened = DetailReducer.Reduce(closed.State, new ChangesFolderToggled(tab, cursor));

        Assert.Equal(["src"], ActiveView(closed).DetailTree.ClosedFolders);
        Assert.Empty(ActiveView(opened).DetailTree.ClosedFolders);
    }

    [Fact]
    public void GivenAFileRowOfTheChangesTree_WhenToggling_ThenNothingChanges()
    {
        var state = WithTab(BottomTab.Changes);

        var update = DetailReducer.Reduce(state, new ChangesFolderToggled(Tab(state), new ChangesCursor(null, 1)));

        Assert.Same(state, update.State);
    }

    [Fact]
    public void GivenAnInnerFolderClosed_WhenTheOuterOneClosesAndOpens_ThenTheInnerOneStaysClosed()
    {
        var state = WithTab(BottomTab.Changes);
        var tab = Tab(state);
        state = DetailReducer.Reduce(state, new ChangesFolderToggled(tab, new ChangesCursor("src/deep", 1))).State;
        state = DetailReducer.Reduce(state, new ChangesFolderToggled(tab, new ChangesCursor("src", -1))).State;
        state = DetailReducer.Reduce(state, new ChangesFolderToggled(tab, new ChangesCursor("src", -1))).State;

        Assert.Equal(["src/deep"], state.Workspace.FindTab(tab)!.View.DetailTree.ClosedFolders);
    }

    [Fact]
    public void GivenAClosedFolder_WhenExpanding_ThenItOpensAndAnOpenOneStaysOpen()
    {
        var state = WithTab(BottomTab.Changes);
        var tab = Tab(state);
        var closed = DetailReducer.Reduce(state, new ChangesFolderToggled(tab, new ChangesCursor("src", -1))).State;

        var opened = DetailReducer.Reduce(closed, new ChangesFolderExpanded(tab, new ChangesCursor("src", -1)));
        var again = DetailReducer.Reduce(opened.State, new ChangesFolderExpanded(tab, new ChangesCursor("src", -1)));

        Assert.Empty(ActiveView(opened).DetailTree.ClosedFolders);
        Assert.Same(opened.State, again.State);
    }

    [Fact]
    public void GivenAnOpenFolder_WhenCollapsingTheChangesTree_ThenItClosesAndTheCursorStays()
    {
        var state = WithTab(BottomTab.Changes);

        var update = DetailReducer.Reduce(state, new ChangesFolderCollapsed(Tab(state), new ChangesCursor("src", -1)));

        Assert.Equal(["src"], ActiveView(update).DetailTree.ClosedFolders);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenAFileInAFolder_WhenCollapsingTheChangesTree_ThenTheCursorGoesToItsFolder()
    {
        var state = WithTab(BottomTab.Changes);

        var update = DetailReducer.Reduce(state, new ChangesFolderCollapsed(Tab(state), new ChangesCursor(null, 1)));

        Assert.Equal(1, Assert.IsType<MoveDetailCursorEffect>(Assert.Single(update.Effects)).Row);
        Assert.Empty(ActiveView(update).DetailTree.ClosedFolders);
    }

    [Fact]
    public void GivenAClosedInnerFolder_WhenCollapsingTheChangesTree_ThenTheCursorGoesToTheOuterFolder()
    {
        var state = WithTab(BottomTab.Changes);
        var tab = Tab(state);
        state = DetailReducer.Reduce(state, new ChangesFolderToggled(tab, new ChangesCursor("src/deep", 1))).State;

        var update = DetailReducer.Reduce(state, new ChangesFolderCollapsed(tab, new ChangesCursor("src/deep", 1)));

        Assert.Equal(1, Assert.IsType<MoveDetailCursorEffect>(Assert.Single(update.Effects)).Row);
    }

    [Fact]
    public void GivenATopLevelFile_WhenCollapsingTheChangesTree_ThenNothingChanges()
    {
        var state = WithTab(BottomTab.Changes);

        var update = DetailReducer.Reduce(state, new ChangesFolderCollapsed(Tab(state), new ChangesCursor(null, -1)));

        Assert.Same(state, update.State);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenTheFilterBoxOpen_WhenAnotherTabBecomesActive_ThenTheBoxClosesAndInsertModeEnds()
    {
        var editing = Editing();
        var first = Tab(editing);

        var opened = Open(editing, "second");
        var switched = AppReducer.Reduce(Open(Editing(), "third"), new PreviousTabCommand());

        Assert.NotEqual(first, opened.Workspace.ActiveTabId);
        Assert.Equal(KeyMode.Normal, opened.Shell.Mode);
        Assert.False(opened.Workspace.FindTab(first)!.View.DetailTree.IsEditingFilter);
        Assert.Equal(KeyMode.Normal, switched.State.Shell.Mode);
    }

    [Fact]
    public void GivenTheFilterBoxOpen_WhenItsTabCloses_ThenInsertModeEnds()
    {
        var state = Editing();

        var update = AppReducer.Reduce(state, new CloseTabCommand(Tab(state)));

        Assert.Equal(KeyMode.Normal, update.State.Shell.Mode);
    }

    private static FileTreeRow Row(FileTreeRowKind kind, string path, bool expanded) =>
        new(kind, path, path, 0, expanded, null, 0);

    private static string NoticeOf(AppUpdate update) =>
        Assert.IsType<PostStatusEffect>(Assert.Single(update.Effects)).Text;

    private static RepositoryViewState ActiveView(AppUpdate update) => update.State.Workspace.ActiveTab!.View;

    private static TabId Tab(AppState state) => state.Workspace.ActiveTabId!.Value;

    private static AppState Open(AppState state, string name)
    {
        var root = Path.Combine(Path.GetTempPath(), $"gitunia-detail-reducer-{name}");
        var gitDirectory = Path.Combine(root, ".git");
        var location = new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);

        return AppReducer.Reduce(state, new OpenRepositoryCommand(location)).State;
    }

    private static AppState WithTab(BottomTab tab)
    {
        var state = Open(AppState.Empty, "first");
        var focused = AppReducer.Reduce(state, new TabCommand(Tab(state), new FocusPaneCommand(PaneKind.Detail))).State;

        return AppReducer.Reduce(focused, new TabCommand(Tab(focused), new SetBottomTabCommand(tab))).State;
    }

    private static AppState WithOpenDirectory(string path)
    {
        var state = WithTab(BottomTab.Files);
        var tree = DetailTreeState.Empty with { OpenDirectories = DetailTreeState.Empty.OpenDirectories.Add(path) };

        return AppReducer.Reduce(state, new TabCommand(Tab(state), new SetDetailTreeCommand(tree))).State;
    }

    private static AppState Editing(string filter = "")
    {
        var state = WithTab(BottomTab.Files);
        var tab = Tab(state);
        state = DetailReducer.Reduce(state, new FileFilterStarted(tab)).State;
        foreach (var character in filter)
        {
            state = DetailReducer.Reduce(state, new FileFilterKeyTyped(tab, new KeyEvent(character.ToString()))).State;
        }

        return state;
    }
}
