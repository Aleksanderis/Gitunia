using Gitunia.Core.Clipboard;
using Gitunia.Core.Keymap;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Staging;
using Gitunia.Git.Status.Models;
using Gitunia.Tui.App;
using Gitunia.Tui.App.Clipboard;
using Gitunia.Tui.App.WorkingTree;
using Gitunia.Tui.Tests.Fakes;
using Xunit;
using static Gitunia.Tui.Tests.Rendering.WorkingTree.WorkingTreeEntries;

namespace Gitunia.Tui.Tests.App.WorkingTree;

public class WorkingTreeActionHandlerTests
{
    private const int PageHeight = 10;

    [Fact]
    public void GivenTheHistory_WhenTogglingTheWorkingTree_ThenItOpensAndTheSecondToggleLeaves()
    {
        var harness = new Harness(Modified("a.txt"));

        Assert.True(harness.Handler.TryExecute(ActionIds.WorkingTreeToggle, null));
        Assert.Equal(TabViewMode.WorkingTree, harness.Tab.View.Mode);
        Assert.Equal(PaneKind.WorkingUnstaged, harness.Tab.View.FocusedPane);

        Assert.True(harness.Handler.TryExecute(ActionIds.WorkingTreeToggle, null));
        Assert.Equal(TabViewMode.History, harness.Tab.View.Mode);
        Assert.Equal(PaneKind.Graph, harness.Tab.View.FocusedPane);
    }

    [Fact]
    public void GivenTheHistory_WhenOpeningTheWorkingTree_ThenItShows()
    {
        var harness = new Harness(Modified("a.txt"));

        Assert.True(harness.Handler.TryExecute(ActionIds.WorkingTreeOpen, null));

        Assert.Equal(TabViewMode.WorkingTree, harness.Tab.View.Mode);
    }

    [Fact]
    public void GivenTheWorkingTreeIsShown_WhenOpeningIt_ThenNothingChangesAndNoNoticeIsSet()
    {
        var harness = new Harness(Modified("a.txt"));
        harness.Handler.TryExecute(ActionIds.WorkingTreeToggle, null);

        Assert.True(harness.Handler.TryExecute(ActionIds.WorkingTreeOpen, null));

        Assert.Equal(TabViewMode.WorkingTree, harness.Tab.View.Mode);
        Assert.Equal(string.Empty, harness.Handler.Message);
    }

    [Fact]
    public void GivenAMissingRepository_WhenOpeningTheWorkingTree_ThenItStaysInTheHistoryWithANotice()
    {
        var harness = new Harness(Modified("a.txt"));
        harness.Send(new SetMissingCommand(true));

        harness.Handler.TryExecute(ActionIds.WorkingTreeToggle, null);

        Assert.Equal(TabViewMode.History, harness.Tab.View.Mode);
        Assert.Contains("missing", harness.Handler.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheHistory_WhenAnyOtherActionArrives_ThenTheHandlerLeavesItToThePanes()
    {
        var harness = new Harness(Modified("a.txt"));

        Assert.False(harness.Handler.TryExecute(ActionIds.NavigateBack, null));
        Assert.False(harness.Handler.TryExecute(ActionIds.ListDown, null));
        Assert.False(harness.Handler.TryExecute(ActionIds.PaneNext, null));
    }

    [Fact]
    public void GivenTheWorkingTree_WhenGoingBack_ThenTheHistoryShows()
    {
        var harness = Harness.Open(Modified("a.txt"));

        Assert.True(harness.Handler.TryExecute(ActionIds.NavigateBack, null));

        Assert.Equal(TabViewMode.History, harness.Tab.View.Mode);
    }

    [Fact]
    public void GivenTheWorkingTree_WhenMovingDownAndUp_ThenTheCursorMovesWithinTheList()
    {
        var harness = Harness.Open(Modified("a.txt"), Modified("b.txt"), Modified("c.txt"));

        harness.Handler.TryExecute(ActionIds.ListDown, null);
        harness.Handler.TryExecute(ActionIds.ListDown, 5);

        Assert.Equal(2, harness.Selection.Index);

        harness.Handler.TryExecute(ActionIds.ListUp, null);
        harness.Handler.TryExecute(ActionIds.ListTop, null);
        harness.Handler.TryExecute(ActionIds.ListBottom, null);

        Assert.Equal("c.txt", harness.Selection.Path);
    }

    [Fact]
    public void GivenTheWorkingTree_WhenPagingDown_ThenTheCursorMovesHalfAPage()
    {
        var harness = Harness.Open([.. Enumerable.Range(0, 30).Select(number => Modified($"f{number:D2}.txt"))]);

        harness.Handler.TryExecute(ActionIds.ListHalfPageDown, null);

        Assert.Equal(PageHeight / 2, harness.Selection.Index);
    }

    [Fact]
    public void GivenTheWorkingTree_WhenCyclingPanes_ThenFocusAlternatesBetweenTheListsAndTheSelectionFollows()
    {
        var harness = Harness.Open(Modified("a.txt"), StagedModified("b.txt"));

        harness.Handler.TryExecute(ActionIds.PaneNext, null);

        Assert.Equal(PaneKind.WorkingStaged, harness.Tab.View.FocusedPane);
        Assert.Equal(WorkingTreeSection.Staged, harness.Selection.Section);

        harness.Handler.TryExecute(ActionIds.PanePrevious, null);

        Assert.Equal(PaneKind.WorkingUnstaged, harness.Tab.View.FocusedPane);
        Assert.Equal(WorkingTreeSection.Unstaged, harness.Selection.Section);
    }

    [Fact]
    public void GivenTheWorkingTree_WhenMovingFocusByDirection_ThenUpAndDownSwitchListsAndRightAndLeftGoToTheDiffAndBack()
    {
        var harness = Harness.Open(Modified("a.txt"), StagedModified("b.txt"));

        harness.Handler.TryExecute(ActionIds.PaneFocusLeft, null);
        Assert.Equal(PaneKind.WorkingUnstaged, harness.Tab.View.FocusedPane);

        harness.Handler.TryExecute(ActionIds.PaneFocusDown, null);
        Assert.Equal(PaneKind.WorkingStaged, harness.Tab.View.FocusedPane);

        harness.Handler.TryExecute(ActionIds.PaneFocusRight, null);
        Assert.Equal(PaneKind.WorkingStagedDiff, harness.Tab.View.FocusedPane);

        harness.Handler.TryExecute(ActionIds.PaneFocusUp, null);
        Assert.Equal(PaneKind.WorkingStagedDiff, harness.Tab.View.FocusedPane);

        harness.Handler.TryExecute(ActionIds.PaneFocusLeft, null);
        Assert.Equal(PaneKind.WorkingStaged, harness.Tab.View.FocusedPane);

        harness.Handler.TryExecute(ActionIds.PaneFocusUp, null);
        Assert.Equal(PaneKind.WorkingUnstaged, harness.Tab.View.FocusedPane);
    }

    [Fact]
    public void GivenAnyPane_WhenJumpingDirectly_ThenEachPaneIsOneActionAway()
    {
        var harness = Harness.Open(Modified("a.txt"), StagedModified("b.txt"));

        harness.Handler.TryExecute(ActionIds.WorkingTreeFocusMessage, null);
        Assert.Equal(PaneKind.WorkingMessage, harness.Tab.View.FocusedPane);

        harness.Handler.TryExecute(ActionIds.WorkingTreeFocusStaged, null);
        Assert.Equal(PaneKind.WorkingStaged, harness.Tab.View.FocusedPane);

        harness.Handler.TryExecute(ActionIds.WorkingTreeFocusDiff, null);
        Assert.Equal(PaneKind.WorkingStagedDiff, harness.Tab.View.FocusedPane);

        harness.Handler.TryExecute(ActionIds.WorkingTreeFocusUnstaged, null);
        Assert.Equal(PaneKind.WorkingUnstaged, harness.Tab.View.FocusedPane);

        harness.Handler.TryExecute(ActionIds.WorkingTreeFocusMessage, null);
        harness.Handler.TryExecute(ActionIds.WorkingTreeFocusDiff, null);
        Assert.Equal(PaneKind.WorkingUnstagedDiff, harness.Tab.View.FocusedPane);
    }

    [Fact]
    public void GivenACleanTree_WhenJumpingToTheDiff_ThenFocusStaysAndANoticeSaysWhy()
    {
        var harness = Harness.Open();

        harness.Handler.TryExecute(ActionIds.WorkingTreeFocusDiff, null);

        Assert.Equal(PaneKind.WorkingUnstaged, harness.Tab.View.FocusedPane);
        Assert.Equal("Select a file to see its diff", harness.Handler.Message);
    }

    [Fact]
    public void GivenTheDiffIsFocused_WhenJumpingToTheDiff_ThenNothingChanges()
    {
        var harness = Harness.Open(Modified("a.txt"));
        harness.Handler.TryExecute(ActionIds.WorkingTreeFocusDiff, null);

        harness.Handler.TryExecute(ActionIds.WorkingTreeFocusDiff, null);

        Assert.Equal(PaneKind.WorkingUnstagedDiff, harness.Tab.View.FocusedPane);
    }

    [Fact]
    public void GivenTheDiff_WhenMovingDownAndUp_ThenTheMessageBoxIsBelowIt()
    {
        var harness = Harness.Open(Modified("a.txt"));
        harness.Handler.TryExecute(ActionIds.PaneFocusRight, null);
        Assert.Equal(PaneKind.WorkingUnstagedDiff, harness.Tab.View.FocusedPane);

        harness.Handler.TryExecute(ActionIds.PaneFocusDown, null);
        Assert.Equal(PaneKind.WorkingMessage, harness.Tab.View.FocusedPane);

        harness.Handler.TryExecute(ActionIds.PaneFocusDown, null);
        Assert.Equal(PaneKind.WorkingMessage, harness.Tab.View.FocusedPane);

        harness.Handler.TryExecute(ActionIds.PaneFocusUp, null);
        Assert.Equal(PaneKind.WorkingUnstagedDiff, harness.Tab.View.FocusedPane);

        harness.Handler.TryExecute(ActionIds.PaneFocusDown, null);
        harness.Handler.TryExecute(ActionIds.PaneFocusLeft, null);
        Assert.Equal(PaneKind.WorkingUnstaged, harness.Tab.View.FocusedPane);
    }

    [Theory]
    [InlineData(false, PaneKind.WorkingUnstagedDiff)]
    [InlineData(true, PaneKind.WorkingStagedDiff)]
    public void GivenAFileUnderTheCursor_WhenOpeningItsDiff_ThenTheDiffOfItsSideTakesFocus(bool isStaged, PaneKind expected)
    {
        var harness = Harness.Open(Modified("a.txt"), StagedModified("b.txt"));
        if (isStaged)
        {
            harness.Handler.TryExecute(ActionIds.PaneNext, null);
        }

        Assert.True(harness.Handler.TryExecute(ActionIds.WorkingTreeOpenDiff, null));

        Assert.Equal(expected, harness.Tab.View.FocusedPane);
        Assert.Equal(TabViewMode.WorkingTree, harness.Tab.View.Mode);
    }

    [Fact]
    public void GivenNoFile_WhenOpeningTheDiff_ThenANoticeAsksForAFile()
    {
        var harness = new Harness();
        harness.Handler.TryExecute(ActionIds.WorkingTreeToggle, null);

        harness.Handler.TryExecute(ActionIds.WorkingTreeOpenDiff, null);

        Assert.Equal("Select a file to see its diff", harness.Handler.Message);
        Assert.Equal(PaneKind.WorkingUnstaged, harness.Tab.View.FocusedPane);
    }

    [Fact]
    public void GivenTheWorkingTree_WhenCyclingPastTheLists_ThenTheDiffOfTheSelectedSideAndTheMessageBoxAreInTheCycle()
    {
        var harness = Harness.Open(Modified("a.txt"), StagedModified("b.txt"));

        harness.Handler.TryExecute(ActionIds.PaneNext, null);
        harness.Handler.TryExecute(ActionIds.PaneNext, null);
        Assert.Equal(PaneKind.WorkingStagedDiff, harness.Tab.View.FocusedPane);

        harness.Handler.TryExecute(ActionIds.PaneNext, null);
        Assert.Equal(PaneKind.WorkingMessage, harness.Tab.View.FocusedPane);

        harness.Handler.TryExecute(ActionIds.PaneNext, null);
        Assert.Equal(PaneKind.WorkingUnstaged, harness.Tab.View.FocusedPane);

        harness.Handler.TryExecute(ActionIds.PanePrevious, null);
        Assert.Equal(PaneKind.WorkingMessage, harness.Tab.View.FocusedPane);
    }

    [Fact]
    public void GivenAnEmptyFocusedList_WhenMoving_ThenTheActionIsHandledAndNothingChanges()
    {
        var harness = Harness.Open(Modified("a.txt"));
        harness.Handler.TryExecute(ActionIds.PaneNext, null);
        var before = harness.Tab;

        Assert.True(harness.Handler.TryExecute(ActionIds.ListDown, null));

        Assert.Same(before, harness.Tab);
    }

    [Fact]
    public void GivenTheStatusIsNotLoaded_WhenMoving_ThenTheActionIsHandled()
    {
        var harness = new Harness();
        harness.Handler.TryExecute(ActionIds.WorkingTreeToggle, null);

        Assert.True(harness.Handler.TryExecute(ActionIds.ListDown, null));
    }

    [Fact]
    public void GivenAnActionOfTheHistory_WhenInTheWorkingTree_ThenItIsSwallowedWithANotice()
    {
        var harness = Harness.Open(Modified("a.txt"));

        Assert.True(harness.Handler.TryExecute(ActionIds.SearchOpen, null));

        Assert.Contains("not available in the working-tree view", harness.Handler.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(LeftToOthers))]
    public void GivenAnInputAction_WhenInTheWorkingTree_ThenTheHandlerLeavesItToThePrompts(string action)
    {
        var harness = Harness.Open(Modified("a.txt"));

        Assert.False(harness.Handler.TryExecute(new(action), null));
    }

    [Fact]
    public void GivenAFileUnderTheCursor_WhenCopyingThePath_ThenTheClipboardReportsIt()
    {
        var harness = Harness.Open(Modified("src/a.txt"));

        harness.Handler.TryExecute(ActionIds.WorkingTreeCopyPath, null);

        Assert.Contains("file path", harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAnEmptyFocusedList_WhenCopyingThePath_ThenTheNoticeAsksForAFile()
    {
        var harness = Harness.Open(Modified("a.txt"));
        harness.Handler.TryExecute(ActionIds.PaneNext, null);

        harness.Handler.TryExecute(ActionIds.WorkingTreeCopyPath, null);

        Assert.Equal("Select a file or folder to copy its path", harness.Handler.Message);
        Assert.Null(harness.Statuses.Log.Latest);
    }

    [Fact]
    public void GivenTheTreeStyle_WhenPressingHRepeatedly_ThenItClosesTheFolderThenClimbsThenLeaves()
    {
        var harness = Harness.Open(Modified("src/deep/a.cs"));
        harness.Send(new TogglePathStyleCommand());
        harness.Send(new TogglePathStyleCommand());
        harness.Send(new SelectWorkingTreeFileCommand(WorkingTreeSection.Unstaged, 2));

        harness.Handler.TryExecute(ActionIds.WorkingTreeCollapse, null);
        Assert.Equal(new WorkingTreeSelection(WorkingTreeSection.Unstaged, "src/deep", 1, true), harness.Selection);
        Assert.Equal(TabViewMode.WorkingTree, harness.Tab.View.Mode);

        harness.Handler.TryExecute(ActionIds.WorkingTreeCollapse, null);
        Assert.Contains("src/deep", harness.Tab.WorkingTree!.CollapsedUnstaged);

        harness.Handler.TryExecute(ActionIds.WorkingTreeCollapse, null);
        Assert.Equal("src", harness.Selection.Path);

        harness.Handler.TryExecute(ActionIds.WorkingTreeCollapse, null);
        harness.Handler.TryExecute(ActionIds.WorkingTreeCollapse, null);
        Assert.Equal(TabViewMode.History, harness.Tab.View.Mode);
    }

    [Fact]
    public void GivenAFolderRow_WhenTogglingAndCopying_ThenTheFolderOpensAndItsPathIsCopied()
    {
        var harness = Harness.Open(Modified("src/a.cs"), Modified("src/b.cs"));
        harness.Send(new TogglePathStyleCommand());
        harness.Send(new TogglePathStyleCommand());
        harness.Send(new SelectWorkingTreeFileCommand(WorkingTreeSection.Unstaged, 0));

        harness.Handler.TryExecute(ActionIds.WorkingTreeToggleFolder, null);
        Assert.Equal(1, harness.Tab.WorkingTree!.RowsOf(WorkingTreeSection.Unstaged).Count);

        harness.Handler.TryExecute(ActionIds.WorkingTreeToggleFolder, null);
        harness.Handler.TryExecute(ActionIds.WorkingTreeCopyPath, null);

        Assert.Equal(3, harness.Tab.WorkingTree.RowsOf(WorkingTreeSection.Unstaged).Count);
        Assert.Contains("folder path", harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    public static TheoryData<string> LeftToOthers() =>
        [
            ActionIds.TabRename.Value,
            ActionIds.InputAccept.Value,
            ActionIds.Cancel.Value,
            ActionIds.AppQuit.Value,
            ActionIds.ToolCommitEditor.Value,
            ActionIds.FilesTogglePathStyle.Value,
        ];

    private sealed class Harness
    {
        private readonly WorkspaceHost _host = new(Workspace.Empty);

        public Harness(params StatusEntry[] entries)
        {
            var root = Path.Combine(Path.GetTempPath(), "gitunia-working-tree-handler-tests");
            var location = new RepositoryLocation(root, Path.Combine(root, ".git"), Path.Combine(root, ".git"), false, false);
            _host.Dispatch(new OpenRepositoryCommand(location));
            if (entries.Length > 0)
            {
                Send(new WorkingTreeLoadedCommand(Snapshot(entries)));
            }

            var writer = new ClipboardWriter(new StringWriter(), new Osc52Options(Osc52Mode.Direct, 100_000));
            Statuses = new StatusChannel();
            var indexWriter = new FakeIndexWriter();
            var staging = new StagingActions(_host, indexWriter, new PatchStager(new FakeDiffReader(), indexWriter), Statuses, action => action(), (_, done) => { done(); return true; }, _ => CancellationToken.None);
            var commits = TestCommitActions.Create(_host, Statuses);
            Handler = new WorkingTreeActionHandler(_host, () => PageHeight, new ClipboardCopier(writer, Statuses), staging, commits);
        }

        public WorkingTreeActionHandler Handler { get; }

        public StatusChannel Statuses { get; }

        public RepositoryState Tab => _host.Workspace.ActiveTab!;

        public WorkingTreeSelection Selection => Tab.WorkingTree!.Selection!;

        public static Harness Open(params StatusEntry[] entries)
        {
            var harness = new Harness(entries);
            harness.Handler.TryExecute(ActionIds.WorkingTreeOpen, null);

            return harness;
        }

        public void Send(RepositoryCommand command) =>
            _host.Dispatch(new TabCommand(_host.Workspace.ActiveTabId!.Value, command));
    }
}
