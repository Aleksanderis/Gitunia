using Gitunia.App.Loading.Commits;
using Gitunia.App.Loading.Trees;
using Gitunia.App.Tabs;
using Gitunia.Core;
using Gitunia.Core.Details;
using Gitunia.Core.Effects;
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
using Terminal.Gui.Views;
using Xunit;

namespace Gitunia.Tui.Tests.App.Details;

public class FileTreeInputTests : IDisposable
{
    private static readonly string[] Paths =
        ["README", "docs/guide.md", "src/lib/core.cs", "src/lib/util.cs", "src/main.cs"];

    private readonly TestShell _shell = new();
    private readonly GraphEntry _entry = CommitFactory.Graph(CommitFactory.Commit(1, "root", []))[0];
    private readonly FakeCommitTreeReader _reader = new();
        private readonly List<string> _opened = [];
    private readonly UiPump _pump = new(TestContext.Current.CancellationToken);
    private readonly TerminalTabSession _session;
    private readonly FileTreeInput _controller;
    private readonly ListView _list;
    private readonly List<string> _notices = [];

    public FileTreeInputTests()
    {
        var root = Path.Combine(Path.GetTempPath(), "gitunia-file-tree-tests");
        var gitDirectory = Path.Combine(root, ".git");
        var location = new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);
        _shell.Host.Dispatch(new OpenRepositoryCommand(location));
        var tabId = _shell.Host.Workspace.ActiveTabId!.Value;
        _shell.Host.Dispatch(new TabCommand(tabId, new SetBottomTabCommand(BottomTab.Files)));
        _shell.Host.Dispatch(new TabCommand(tabId, new FocusPaneCommand(PaneKind.Detail)));
        _reader.Add(_entry.Id, Paths);
        var debounce = TimeSpan.FromMilliseconds(1);
        var store = new CommitTreeStore(_reader, FakeCommitDetailsReader.Location, _pump, debounce);
        _session = new TerminalTabSession(
            new TabSessionParts(
                tabId,
                new CommitDetailsStore(
                new FakeCommitDetailsReader(),
                FakeCommitDetailsReader.Location,
                _pump,
                debounce),
                Tree: new FileTreeSession(store)),
            GraphGlyphSet.Unicode);
        _list = new ListView { Source = _session.DetailSource };
        _controller = new FileTreeInput(_shell.Host, _list);
        _shell.Host.EffectRaised += OnEffect;
        _shell.Host.Changed += Refresh;
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

        Cursor = 0;
        _controller.TryExecute(ActionIds.DetailOpenDiff, null);

        Assert.Equal(["docs/", "src/", "README"], Labels());
    }

    [Fact]
    public async Task GivenADirectoryOpenedOnTheCursor_WhenOpening_ThenTheContentListsItsChildren()
    {
        await LoadAsync();
        Cursor = 1;

        _controller.TryExecute(ActionIds.DetailOpenDiff, null);

        Assert.Equal(["docs/", "src/", "  lib/", "  main.cs", "README"], Labels());
    }

    [Fact]
    public async Task GivenAFileRow_WhenOpening_ThenTheFileIsHandedToTheContentView()
    {
        await LoadAsync();
        Cursor = 2;

        var handled = _controller.TryExecute(ActionIds.DetailOpenDiff, null);

        Assert.True(handled);
        Assert.Equal(["README"], _opened);
        Assert.Equal(["docs/", "src/", "README"], Labels());
    }

    [Fact]
    public async Task GivenAnOpenDirectory_WhenPressingH_ThenItClosesAndTheCursorStays()
    {
        await LoadAsync();
        Cursor = 1;
        _controller.TryExecute(ActionIds.DetailOpenDiff, null);

        _controller.TryExecute(ActionIds.DetailCollapse, null);

        Assert.Equal(["docs/", "src/", "README"], Labels());
        Assert.Equal(1, Cursor);
    }

    [Fact]
    public async Task GivenARowInsideADirectory_WhenPressingH_ThenTheCursorGoesToTheParent()
    {
        await LoadAsync();
        Cursor = 1;
        _controller.TryExecute(ActionIds.DetailOpenDiff, null);
        Cursor = 3;

        _controller.TryExecute(ActionIds.DetailCollapse, null);

        Assert.Equal(1, Cursor);
        Assert.Equal(["docs/", "src/", "  lib/", "  main.cs", "README"], Labels());
    }

    [Fact]
    public async Task GivenATopLevelRow_WhenPressingH_ThenNothingMovesAndFocusStays()
    {
        await LoadAsync();
        Cursor = 2;

        _controller.TryExecute(ActionIds.DetailCollapse, null);
        Cursor = 0;
        _controller.TryExecute(ActionIds.DetailCollapse, null);

        Assert.Equal(0, Cursor);
        Assert.Equal(PaneKind.Detail, _shell.Host.Workspace.ActiveTab!.View.FocusedPane);
        Assert.Equal(["docs/", "src/", "README"], Labels());
    }

    [Fact]
    public async Task GivenAClosedDirectory_WhenPressingL_ThenItOpensWithoutLeavingThePane()
    {
        await LoadAsync();
        Cursor = 1;

        _controller.TryExecute(ActionIds.DetailExpand, null);
        var afterFirst = Labels();
        _controller.TryExecute(ActionIds.DetailExpand, null);

        Assert.Equal(["docs/", "src/", "  lib/", "  main.cs", "README"], afterFirst);
        Assert.Equal(afterFirst, Labels());
        Assert.Equal(PaneKind.Detail, _shell.Host.Workspace.ActiveTab!.View.FocusedPane);
    }

    [Fact]
    public async Task GivenAFileRow_WhenPressingL_ThenTheFileIsNotOpened()
    {
        await LoadAsync();
        Cursor = 2;

        _controller.TryExecute(ActionIds.DetailExpand, null);

        Assert.Empty(_opened);
    }

    [Theory]
    [InlineData(BottomTab.Details)]
    [InlineData(BottomTab.Changes)]
    public async Task GivenAnotherTab_WhenTheFilesActionsRun_ThenTheyAreNotHandled(BottomTab tab)
    {
        await LoadAsync();
        _shell.Host.Dispatch(new TabCommand(_session.TabId, new SetBottomTabCommand(tab)));

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
        Cursor = 2;

        _controller.TryExecute(ActionIds.DetailFilter, null);
        Assert.Equal([KeyMode.Insert], _shell.Modes);
        TypeText("LIB");

        Assert.Equal(["src/lib/", "  core.cs", "  util.cs"], Labels());
        Assert.Equal(0, Cursor);
        Assert.Equal("/LIB", _controller.StatusLine);
    }

    [Fact]
    public async Task GivenAFilter_WhenAccepting_ThenItStaysAndInsertModeEnds()
    {
        await LoadAsync();
        _controller.TryExecute(ActionIds.DetailFilter, null);
        TypeText("main");

        _controller.TryExecute(ActionIds.InputAccept, null);

        Assert.Equal([KeyMode.Insert, KeyMode.Normal], _shell.Modes);
        Assert.Equal(["src/", "  main.cs"], Labels());
        Assert.Equal("filter: main", _controller.StatusLine);
        Assert.False(_shell.Host.Workspace.ActiveTab!.View.DetailTree.IsEditingFilter);
    }

    [Fact]
    public async Task GivenAFilterBeingTyped_WhenCancelling_ThenTheFilterIsClearedAndTheFullTreeReturns()
    {
        await LoadAsync();
        _controller.TryExecute(ActionIds.DetailFilter, null);
        TypeText("main");

        _controller.TryExecute(ActionIds.Cancel, null);

        Assert.Equal([KeyMode.Insert, KeyMode.Normal], _shell.Modes);
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
        Cursor = 0;

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
        _shell.Host.Dispatch(new TabCommand(_session.TabId, new SetBottomTabCommand(BottomTab.Changes)));

        var handled = _controller.TryExecute(ActionIds.DetailFilter, null);

        Assert.True(handled);
        Assert.False(_shell.Host.Workspace.ActiveTab!.View.DetailTree.IsEditingFilter);
        Assert.Empty(_shell.Modes);
        Assert.Equal(DetailReducer.WrongTabNotice, Assert.Single(_notices));
    }

    [Fact]
    public async Task GivenAFilterLeftOpenOnAnotherBottomTab_WhenTyping_ThenTheKeysAreNotConsumed()
    {
        await LoadAsync();
        _controller.TryExecute(ActionIds.DetailFilter, null);
        _shell.Host.Dispatch(new TabCommand(_session.TabId, new SetBottomTabCommand(BottomTab.Changes)));

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
        Cursor = 1;
        _controller.TryExecute(ActionIds.DetailOpenDiff, null);
        var other = CommitFactory.Id(2);
        _reader.Add(other, "src/other.cs", "top.txt");
        _session.Tree!.Store.Select(other);
        await _pump.RunUntilAsync(
            () => _session.Tree.Store.Current is { Commit: { } commit, Status: CommitDetailsStatus.Loaded }
                && commit == other);

        Assert.Equal(["src", "other.cs", "top.txt"], _session.Tree.RowsFor(_shell.Host.Workspace.ActiveTab!.View.DetailTree).Select(row => row.Label));
    }

    public void Dispose()
    {
        _list.Dispose();
        _session.Dispose();
        GC.SuppressFinalize(this);
    }

    private int Cursor
    {
        get => _list.SelectedItem ?? 0;
        set => _list.SelectedItem = value;
    }

    private void OnEffect(AppEffect effect)
    {
        if (effect is MoveDetailCursorEffect move && move.Row < (_list.Source?.Count ?? 0))
        {
            Cursor = move.Row;
        }
        else if (effect is OpenFileContentEffect open)
        {
            _opened.Add(open.Row.Path);
        }
        else if (effect is PostStatusEffect post)
        {
            _notices.Add(post.Text);
        }
    }

    private async Task LoadAsync()
    {
        _session.Tree!.Store.Select(_entry.Id);
        await _pump.RunUntilAsync(() => _session.Tree.Store.Current.Status == CommitDetailsStatus.Loaded);
        Refresh();
        Cursor = 0;
    }

    // What the application does after the tree changed: rebuild the detail content from the session.
    private void Refresh()
    {
        var tree = _session.Tree!;
        var state = _shell.Host.Workspace.ActiveTab!.View.DetailTree;
        var view = new FileTreeView(tree.Store.Current, tree.RowsFor(state), state.Filter, SidebarGlyphs.Unicode);
        var tab = _shell.Host.Workspace.ActiveTab!.View.BottomTab;
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
