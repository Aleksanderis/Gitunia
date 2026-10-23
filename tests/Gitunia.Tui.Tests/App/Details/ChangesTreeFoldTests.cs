using Gitunia.Core.Graph;
using Gitunia.Core.Keymap;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Tui.App;
using Gitunia.Tui.App.Details;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Loading.Commits;
using Gitunia.Tui.Rendering.Details;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Tests.Fakes;
using Gitunia.Tui.Tests.Rendering.Graph;
using Xunit;

namespace Gitunia.Tui.Tests.App.Details;

public class ChangesTreeFoldTests : IDisposable
{
    // Rows: 0 heading, 1 src/, 2 deep/, 3 x.cs, 4 a.cs, 5 b.cs, 6 top.txt.
    private static readonly CommitFileChange[] Changes =
    [
        new("src/deep/x.cs", null, FileChangeKind.Added, null),
        new("src/a.cs", null, FileChangeKind.Modified, null),
        new("src/b.cs", null, FileChangeKind.Modified, null),
        new("top.txt", null, FileChangeKind.Modified, null),
    ];

    private readonly WorkspaceHost _host = new(Workspace.Empty);
    private readonly GraphEntry _entry = CommitFactory.Graph(CommitFactory.Commit(1, "root", []))[0];
    private readonly UiPump _pump = new(TestContext.Current.CancellationToken);
    private readonly TabSession _session;
    private readonly FileTreeController _controller;
    private int _cursor;

    public ChangesTreeFoldTests()
    {
        var root = Path.Combine(Path.GetTempPath(), "gitunia-changes-fold-tests");
        var gitDirectory = Path.Combine(root, ".git");
        var location = new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);
        _host.Dispatch(new OpenRepositoryCommand(location));
        var tabId = _host.Workspace.ActiveTabId!.Value;
        _host.Dispatch(new TabCommand(tabId, new SetBottomTabCommand(BottomTab.Changes)));
        _host.Dispatch(new TabCommand(tabId, new FocusPaneCommand(PaneKind.Detail)));
        while (_host.Workspace.ActiveTab!.View.PathStyle != FilePathStyle.Tree)
        {
            _host.Dispatch(new TabCommand(tabId, new TogglePathStyleCommand()));
        }

        _session = new TabSession(
            tabId,
            GraphGlyphSet.Unicode,
            new CommitDetailsStore(
                new FakeCommitDetailsReader(),
                FakeCommitDetailsReader.Location,
                _pump.Post,
                TimeSpan.FromMilliseconds(1)));
        _controller = new FileTreeController(
            _host,
            () => _session,
            () => _cursor,
            row => _cursor = row,
            _ => { },
            Refresh,
            _ => string.Empty);
        Refresh();
    }

    [Fact]
    public void GivenAnOpenFolder_WhenTogglingIt_ThenItsRowsGoAndItShowsTheFileCount()
    {
        _cursor = 1;

        var handled = _controller.TryExecute(ActionIds.DetailToggleFolder, null);

        Assert.True(handled);
        Assert.Equal(["src/  3 files", "M top.txt"], Outline());
        Assert.Equal(1, _cursor);
    }

    [Fact]
    public void GivenAClosedFolder_WhenTogglingIt_ThenItsRowsReturn()
    {
        _cursor = 1;
        _controller.TryExecute(ActionIds.DetailToggleFolder, null);

        _controller.TryExecute(ActionIds.DetailToggleFolder, null);

        Assert.Equal(["src/", "deep/", "A x.cs", "M a.cs", "M b.cs", "M top.txt"], Outline());
    }

    [Fact]
    public void GivenAFileRow_WhenToggling_ThenNothingChanges()
    {
        _cursor = 3;

        _controller.TryExecute(ActionIds.DetailToggleFolder, null);

        Assert.Empty(_session.CollapsedFolders);
        Assert.Equal(7, _session.DetailSource.Content.Rows.Count);
    }

    [Fact]
    public void GivenAnInnerFolderClosed_WhenTheOuterOneClosesAndOpens_ThenTheInnerOneStaysClosed()
    {
        _cursor = 2;
        _controller.TryExecute(ActionIds.DetailToggleFolder, null);
        _cursor = 1;
        _controller.TryExecute(ActionIds.DetailToggleFolder, null);
        _controller.TryExecute(ActionIds.DetailToggleFolder, null);

        Assert.Equal(["src/", "deep/  1 file", "M a.cs", "M b.cs", "M top.txt"], Outline());
    }

    [Fact]
    public void GivenAnOpenFolder_WhenPressingH_ThenItClosesAndTheCursorStays()
    {
        _cursor = 1;

        Assert.Equal(ActionIds.DetailCollapse, _controller.Resolve(ActionIds.DetailCollapse));
        _controller.TryExecute(ActionIds.DetailCollapse, null);

        Assert.Equal(["src/  3 files", "M top.txt"], Outline());
        Assert.Equal(1, _cursor);
    }

    [Fact]
    public void GivenAFileInAFolder_WhenPressingH_ThenTheCursorGoesToItsFolder()
    {
        _cursor = 4;

        _controller.TryExecute(ActionIds.DetailCollapse, null);

        Assert.Equal(1, _cursor);
        Assert.Empty(_session.CollapsedFolders);
    }

    [Fact]
    public void GivenAClosedInnerFolder_WhenPressingH_ThenTheCursorGoesToTheOuterFolder()
    {
        _cursor = 2;
        _controller.TryExecute(ActionIds.DetailToggleFolder, null);

        _controller.TryExecute(ActionIds.DetailCollapse, null);

        Assert.Equal(1, _cursor);
    }

    [Fact]
    public void GivenATopLevelFile_WhenPressingH_ThenItGoesBackToTheGraph()
    {
        _cursor = 6;

        Assert.Equal(ActionIds.NavigateBack, _controller.Resolve(ActionIds.DetailCollapse));
    }

    [Fact]
    public void GivenAClosedTopLevelFolder_WhenPressingH_ThenItGoesBackToTheGraph()
    {
        _cursor = 1;
        _controller.TryExecute(ActionIds.DetailToggleFolder, null);

        Assert.Equal(ActionIds.NavigateBack, _controller.Resolve(ActionIds.DetailCollapse));
    }

    [Fact]
    public void GivenAFlatStyle_WhenToggling_ThenTheActionIsNotHandled()
    {
        _host.Dispatch(new TabCommand(_session.TabId, new TogglePathStyleCommand()));

        Assert.False(_controller.TryExecute(ActionIds.DetailToggleFolder, null));
    }

    [Fact]
    public void GivenAFileHiddenByAClosedFolder_WhenTheStyleChangesToTree_ThenTheCursorLandsOnTheClosedFolder()
    {
        var collapsed = new HashSet<string> { "src" };
        var flat = Build(FilePathStyle.NameFirst, collapsed);
        var tree = Build(FilePathStyle.Tree, collapsed);

        var row = DetailCursorRemap.Row(flat, tree, flat.RowOfFile(2));

        Assert.Equal("src", tree.FolderAt(row));
    }

    [Fact]
    public void GivenAnOpenOuterAndClosedInnerFolder_WhenTheStyleChangesToTree_ThenTheCursorLandsOnTheInnerFolder()
    {
        var collapsed = new HashSet<string> { "src/deep" };
        var flat = Build(FilePathStyle.NameFirst, collapsed);
        var tree = Build(FilePathStyle.Tree, collapsed);

        var row = DetailCursorRemap.Row(flat, tree, flat.RowOfFile(0));

        Assert.Equal("src/deep", tree.FolderAt(row));
    }

    public void Dispose()
    {
        _session.Dispose();
        GC.SuppressFinalize(this);
    }

    private DetailContent Build(FilePathStyle style, IReadOnlySet<string> collapsed) =>
        DetailContentBuilder.Build(_entry, State(), BottomTab.Changes, null, style, null, collapsed);

    private void Refresh() => _session.DetailSource.Update(Build(FilePathStyle.Tree, _session.CollapsedFolders));

    private CommitDetailsState State() => new(
        _entry.Id,
        CommitDetailsStatus.Loaded,
        new CommitDetails(
            _entry.Id,
            [],
            "Ann",
            "ann@x.org",
            DateTimeOffset.UnixEpoch,
            "Ann",
            "ann@x.org",
            DateTimeOffset.UnixEpoch,
            "m",
            Changes),
        string.Empty);

    // Row texts without the heading, indentation trimmed.
    private string[] Outline() =>
    [
        .. _session.DetailSource.Content.Rows
            .Skip(1)
            .Select(row => string.Concat(row.Segments.Select(segment => segment.Text)).Trim()),
    ];
}
