using Gitunia.Core;
using Gitunia.App;
using Gitunia.App.Loading.Commits;
using Gitunia.App.Loading.Trees;
using Gitunia.App.Tabs;
using Gitunia.Core.FileTree;
using Gitunia.Core.Graph;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Repositories.Models;
using Gitunia.Tui.App.Details;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Rendering.Details;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Sidebar;
using Gitunia.Tui.Tests.Fakes;
using Gitunia.Tui.Tests.Rendering.Graph;
using Xunit;

namespace Gitunia.Tui.Tests.App.Details;

public class FileTreeControllerTests : IDisposable
{
    private static readonly string[] Paths =
        ["README", "docs/guide.md", "src/lib/core.cs", "src/lib/util.cs", "src/main.cs"];

    private readonly Dispatcher _host = new(AppState.Empty);
    private readonly GraphEntry _entry = CommitFactory.Graph(CommitFactory.Commit(1, "root", []))[0];
    private readonly FakeCommitTreeReader _reader = new();
    private readonly List<KeyMode> _modes = [];
    private readonly List<string> _opened = [];
    private readonly UiPump _pump = new(TestContext.Current.CancellationToken);
    private readonly TerminalTabSession _session;
    private readonly FileTreeController _controller;
    private int _cursor;

    public FileTreeControllerTests()
    {
        var root = Path.Combine(Path.GetTempPath(), "gitunia-file-tree-tests");
        var gitDirectory = Path.Combine(root, ".git");
        var location = new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);
        _host.Dispatch(new OpenRepositoryCommand(location));
        var tabId = _host.Workspace.ActiveTabId!.Value;
        _host.Dispatch(new TabCommand(tabId, new SetBottomTabCommand(BottomTab.Files)));
        _host.Dispatch(new TabCommand(tabId, new FocusPaneCommand(PaneKind.Detail)));
        _reader.Add(_entry.Id, Paths);
        var debounce = TimeSpan.FromMilliseconds(1);
        var store = new CommitTreeStore(_reader, FakeCommitDetailsReader.Location, _pump.Post, debounce);
        _session = new TerminalTabSession(
            new TabSessionParts(
                tabId,
                new CommitDetailsStore(
                new FakeCommitDetailsReader(),
                FakeCommitDetailsReader.Location,
                _pump.Post,
                debounce),
                Tree: new FileTreeSession(store)),
            GraphGlyphSet.Unicode);
        _controller = new FileTreeController(
            _host,
            () => _session,
            () => _cursor,
            row => _cursor = row,
            _modes.Add,
            Refresh,
            row =>
            {
                _opened.Add(row.Path);
                return "opened";
            });
        Refresh();
    }

    [Fact]
    public async Task GivenAClosedDirectory_WhenOpening_ThenItsChildrenAppear()
    {
        await LoadAsync();

        var handled = _controller.TryExecute(ActionIds.DetailOpenDiff, null);

        Assert.True(handled);
        Assert.Equal(["docs/", "  guide.md", "src/", "README"], Labels());
    }

    [Fact]
    public async Task GivenAnOpenDirectory_WhenOpeningAgain_ThenItCloses()
    {
        await LoadAsync();
        _controller.TryExecute(ActionIds.DetailOpenDiff, null);

        _cursor = 0;
        _controller.TryExecute(ActionIds.DetailOpenDiff, null);

        Assert.Equal(["docs/", "src/", "README"], Labels());
    }

    [Fact]
    public async Task GivenADirectoryOpenedOnTheCursor_WhenOpening_ThenTheContentListsItsChildren()
    {
        await LoadAsync();
        _cursor = 1;

        _controller.TryExecute(ActionIds.DetailOpenDiff, null);

        Assert.Equal(["docs/", "src/", "  lib/", "  main.cs", "README"], Labels());
    }

    [Fact]
    public async Task GivenAFileRow_WhenOpening_ThenTheFileIsHandedToTheContentViewAndItsNoticeShows()
    {
        await LoadAsync();
        _cursor = 2;

        var handled = _controller.TryExecute(ActionIds.DetailOpenDiff, null);

        Assert.True(handled);
        Assert.Equal(["README"], _opened);
        Assert.Equal("opened", _controller.Notice);
        Assert.Equal(["docs/", "src/", "README"], Labels());
    }

    [Fact]
    public async Task GivenAnOpenDirectory_WhenPressingH_ThenItClosesAndTheCursorStays()
    {
        await LoadAsync();
        _cursor = 1;
        _controller.TryExecute(ActionIds.DetailOpenDiff, null);

        _controller.TryExecute(ActionIds.DetailCollapse, null);

        Assert.Equal(["docs/", "src/", "README"], Labels());
        Assert.Equal(1, _cursor);
    }

    [Fact]
    public async Task GivenARowInsideADirectory_WhenPressingH_ThenTheCursorGoesToTheParent()
    {
        await LoadAsync();
        _cursor = 1;
        _controller.TryExecute(ActionIds.DetailOpenDiff, null);
        _cursor = 3;

        _controller.TryExecute(ActionIds.DetailCollapse, null);

        Assert.Equal(1, _cursor);
        Assert.Equal(["docs/", "src/", "  lib/", "  main.cs", "README"], Labels());
    }

    [Fact]
    public async Task GivenATopLevelRow_WhenPressingH_ThenNothingMovesAndFocusStays()
    {
        await LoadAsync();
        _cursor = 2;

        _controller.TryExecute(ActionIds.DetailCollapse, null);
        _cursor = 0;
        _controller.TryExecute(ActionIds.DetailCollapse, null);

        Assert.Equal(0, _cursor);
        Assert.Equal(PaneKind.Detail, _host.Workspace.ActiveTab!.View.FocusedPane);
        Assert.Equal(["docs/", "src/", "README"], Labels());
    }

    [Fact]
    public async Task GivenAClosedDirectory_WhenPressingL_ThenItOpensWithoutLeavingThePane()
    {
        await LoadAsync();
        _cursor = 1;

        _controller.TryExecute(ActionIds.DetailExpand, null);
        var afterFirst = Labels();
        _controller.TryExecute(ActionIds.DetailExpand, null);

        Assert.Equal(["docs/", "src/", "  lib/", "  main.cs", "README"], afterFirst);
        Assert.Equal(afterFirst, Labels());
        Assert.Equal(PaneKind.Detail, _host.Workspace.ActiveTab!.View.FocusedPane);
    }

    [Fact]
    public async Task GivenAFileRow_WhenPressingL_ThenTheFileIsNotOpened()
    {
        await LoadAsync();
        _cursor = 2;

        _controller.TryExecute(ActionIds.DetailExpand, null);

        Assert.Empty(_opened);
    }

    [Theory]
    [InlineData(BottomTab.Details)]
    [InlineData(BottomTab.Changes)]
    public async Task GivenAnotherTab_WhenTheFilesActionsRun_ThenTheyAreNotHandled(BottomTab tab)
    {
        await LoadAsync();
        _host.Dispatch(new TabCommand(_session.TabId, new SetBottomTabCommand(tab)));

        Assert.False(_controller.TryExecute(ActionIds.DetailOpenDiff, null));
        Assert.False(_controller.TryExecute(ActionIds.DetailExpand, null));
    }

    [Fact]
    public async Task GivenOtherActions_WhenExecuting_ThenTheyAreNotHandled()
    {
        await LoadAsync();

        Assert.False(_controller.TryExecute(ActionIds.ListDown, null));
    }

    [Fact]
    public async Task GivenTheFilterKey_WhenTypingAPath_ThenTheRowsNarrowAsYouTypeAndTheCursorGoesToTheTop()
    {
        await LoadAsync();
        _cursor = 2;

        _controller.TryExecute(ActionIds.DetailFilter, null);
        Assert.Equal([KeyMode.Insert], _modes);
        TypeText("LIB");

        Assert.Equal(["src/lib/", "  core.cs", "  util.cs"], Labels());
        Assert.Equal(0, _cursor);
        Assert.Equal("/LIB", _controller.StatusLine);
    }

    [Fact]
    public async Task GivenAFilter_WhenAccepting_ThenItStaysAndInsertModeEnds()
    {
        await LoadAsync();
        _controller.TryExecute(ActionIds.DetailFilter, null);
        TypeText("main");

        _controller.TryExecute(ActionIds.InputAccept, null);

        Assert.Equal([KeyMode.Insert, KeyMode.Normal], _modes);
        Assert.Equal(["src/", "  main.cs"], Labels());
        Assert.Equal("filter: main", _controller.StatusLine);
        Assert.False(_session.Tree!.IsEditingFilter);
    }

    [Fact]
    public async Task GivenAFilterBeingTyped_WhenCancelling_ThenTheFilterIsClearedAndTheFullTreeReturns()
    {
        await LoadAsync();
        _controller.TryExecute(ActionIds.DetailFilter, null);
        TypeText("main");

        _controller.TryExecute(ActionIds.Cancel, null);

        Assert.Equal([KeyMode.Insert, KeyMode.Normal], _modes);
        Assert.Equal(["docs/", "src/", "README"], Labels());
        Assert.Equal(string.Empty, _controller.StatusLine);
    }

    [Fact]
    public async Task GivenAKeptFilter_WhenGoingBackInNormalMode_ThenTheFilterClearsFirst()
    {
        await LoadAsync();
        _controller.TryExecute(ActionIds.DetailFilter, null);
        TypeText("main");
        _controller.TryExecute(ActionIds.InputAccept, null);

        var cleared = _controller.TryExecute(ActionIds.NavigateBack, null);
        var again = _controller.TryExecute(ActionIds.NavigateBack, null);

        Assert.True(cleared);
        Assert.False(again);
        Assert.Equal(["docs/", "src/", "README"], Labels());
    }

    [Fact]
    public async Task GivenAFilter_WhenOpeningADirectory_ThenItStaysOpenAndNothingChanges()
    {
        await LoadAsync();
        _controller.TryExecute(ActionIds.DetailFilter, null);
        TypeText("lib");
        _controller.TryExecute(ActionIds.InputAccept, null);
        _cursor = 0;

        _controller.TryExecute(ActionIds.DetailOpenDiff, null);

        Assert.Equal(["src/lib/", "  core.cs", "  util.cs"], Labels());
    }

    [Fact]
    public async Task GivenAFilterWithNoMatch_WhenTyping_ThenThePaneSaysSo()
    {
        await LoadAsync();
        _controller.TryExecute(ActionIds.DetailFilter, null);

        TypeText("zzz");

        Assert.Equal(["No file matches the filter"], Labels());
        Assert.Empty(_session.DetailSource.Content.Tree);
    }

    [Fact]
    public async Task GivenAFilterOnAnotherTab_WhenPressingTheFilterKey_ThenItOnlyExplains()
    {
        await LoadAsync();
        _host.Dispatch(new TabCommand(_session.TabId, new SetBottomTabCommand(BottomTab.Changes)));

        var handled = _controller.TryExecute(ActionIds.DetailFilter, null);

        Assert.True(handled);
        Assert.False(_session.Tree!.IsEditingFilter);
        Assert.Empty(_modes);
        Assert.Contains("Files tab", _controller.Notice, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAFilterLeftOpenOnAnotherBottomTab_WhenTyping_ThenTheKeysAreNotConsumed()
    {
        await LoadAsync();
        _controller.TryExecute(ActionIds.DetailFilter, null);
        _host.Dispatch(new TabCommand(_session.TabId, new SetBottomTabCommand(BottomTab.Changes)));

        Assert.False(_controller.HandleTextKey(new KeyEvent("a")));
        Assert.False(_controller.TryExecute(ActionIds.InputAccept, null));
    }

    [Fact]
    public async Task GivenNoFilterBox_WhenTypingKeys_ThenTheyAreNotConsumed()
    {
        await LoadAsync();

        Assert.False(_controller.HandleTextKey(new KeyEvent("a")));
    }

    [Fact]
    public async Task GivenOpenedDirectories_WhenAnotherCommitIsSelected_ThenThePathStaysOpen()
    {
        await LoadAsync();
        _cursor = 1;
        _controller.TryExecute(ActionIds.DetailOpenDiff, null);
        var other = CommitFactory.Id(2);
        _reader.Add(other, "src/other.cs", "top.txt");
        _session.Tree!.Store.Select(other);
        await _pump.RunUntilAsync(
            () => _session.Tree.Store.Current is { Commit: { } commit, Status: CommitDetailsStatus.Loaded }
                && commit == other);

        Assert.Equal(["src", "other.cs", "top.txt"], _session.Tree.Rows.Select(row => row.Label));
    }

    public void Dispose()
    {
        _session.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task LoadAsync()
    {
        _session.Tree!.Store.Select(_entry.Id);
        await _pump.RunUntilAsync(() => _session.Tree.Store.Current.Status == CommitDetailsStatus.Loaded);
        Refresh();
    }

    // What the application does after the tree changed: rebuild the detail content from the session.
    private void Refresh()
    {
        var tree = _session.Tree!;
        var view = new FileTreeView(tree.Store.Current, tree.Rows, tree.Filter, SidebarGlyphs.Unicode);
        var tab = _host.Workspace.ActiveTab!.View.BottomTab;
        _session.DetailSource.Update(DetailContentBuilder.Build(_entry, CommitDetailsState.Empty, tab, view));
    }

    private void TypeText(string text)
    {
        foreach (var character in text)
        {
            Assert.True(_controller.HandleTextKey(new KeyEvent(character.ToString())));
        }
    }

    private static string SlashOf(FileTreeRow row) => row.Kind == FileTreeRowKind.Directory ? "/" : string.Empty;

    // The visible tree rows as an indented outline (two spaces per level, directories end in a slash), or the pane's
    // message.
    private string[] Labels()
    {
        var content = _session.DetailSource.Content;
        if (content.Tree.Count == 0)
        {
            return [.. content.Rows.Select(row => string.Concat(row.Segments.Select(segment => segment.Text)))];
        }

        return
        [
            .. content.Tree.Select(row =>
                $"{new string(' ', row.Depth * 2)}{row.Label}{SlashOf(row)}"),
        ];
    }
}
