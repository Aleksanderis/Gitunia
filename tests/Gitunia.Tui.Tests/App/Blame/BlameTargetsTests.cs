using Gitunia.App.Loading.Commits;
using Gitunia.App.Tabs;
using Gitunia.Core;
using Gitunia.Core.FileTree;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Status.Models;
using Gitunia.Tui.App.Blame;
using Gitunia.Tui.App.Rebase;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Rendering.Details;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Tests.Fakes;
using Terminal.Gui.Views;
using Xunit;

namespace Gitunia.Tui.Tests.App.Blame;

// Which file the blame key stands for: the details pane at the shown commit, the Changes view in the working tree.
public sealed class BlameTargetsTests : IDisposable
{
    private const string Commit = "1111111111111111111111111111111111111111";

    private readonly TerminalTabSession _session = new(
        new TabSessionParts(
            new TabId(1),
            new CommitDetailsStore(
                TestGit.Services.CommitDetails,
                new RepositoryLocation("/x", "/x/.git", "/x/.git", false, false),
                InlineUi.Instance,
                TimeSpan.Zero)),
        GraphGlyphSet.Unicode);

    private readonly ListView _detail = new();
    private readonly RepositoryState _tab = AppReducer
        .Reduce(AppState.Empty, new OpenRepositoryCommand(new RepositoryLocation("/x", "/x/.git", "/x/.git", false, false)))
        .State.Workspace.Tabs[0];

    private string? _selectedCommit = Commit;

    public BlameTargetsTests()
    {
        _detail.Source = _session.DetailSource;
    }

    [Fact]
    public void GivenAChangedFileRow_WhenResolving_ThenTheFileIsBlamedAtTheSelectedCommit()
    {
        ShowChanges(Change("src/a.txt", FileChangeKind.Modified), Change("b.txt", FileChangeKind.Added));

        var target = Targets().Resolve(_tab);

        Assert.Equal(("b.txt", Commit), (target.Path, target.Revision));
        Assert.Equal(string.Empty, target.Notice);
    }

    [Fact]
    public void GivenADeletedFileRow_WhenResolving_ThenThereIsNothingToBlame()
    {
        ShowChanges(Change("gone.txt", FileChangeKind.Deleted), Change("b.txt", FileChangeKind.Added));
        _detail.SelectedItem = 1;

        var target = Targets().Resolve(_tab);

        Assert.Null(target.Path);
        Assert.Equal(BlameTargets.DeletedNotice, target.Notice);
    }

    [Fact]
    public void GivenTheHeadingRowOrNoCommit_WhenResolving_ThenTheNoticeSaysWhatToSelect()
    {
        ShowChanges(Change("a.txt", FileChangeKind.Modified));
        _detail.SelectedItem = 0;
        var heading = Targets().Resolve(_tab);
        _detail.SelectedItem = 1;
        _selectedCommit = null;
        var noCommit = Targets().Resolve(_tab);

        Assert.Equal(BlameTargets.NoFileNotice, heading.Notice);
        Assert.Equal(BlameTargets.NoCommitNotice, noCommit.Notice);
    }

    [Fact]
    public void GivenTheFilesTab_WhenResolving_ThenAFileRowIsBlamedAndADirectoryRowIsRefused()
    {
        var tree = new[]
        {
            new FileTreeRow(FileTreeRowKind.Directory, "src", "src", 0, true, null, 1),
            new FileTreeRow(FileTreeRowKind.File, "src/a.txt", "a.txt", 1, false, 3, 0),
        };
        _session.DetailSource.Update(new DetailContent("abc", [new DetailRow([]), new DetailRow([])], 2, [], BottomTab.Files)
        {
            Tree = tree,
        });

        _detail.SelectedItem = 0;
        var directory = Targets().Resolve(_tab);
        _detail.SelectedItem = 1;
        var file = Targets().Resolve(_tab);

        Assert.Equal(BlameTargets.NoFileNotice, directory.Notice);
        Assert.Equal(("src/a.txt", Commit), (file.Path, file.Revision));
    }

    [Fact]
    public void GivenTheChangesViewWithAnUnstagedFile_WhenResolving_ThenTheWorkingTreeFileIsBlamedWithoutARevision()
    {
        var tab = InWorkingTree(PaneKind.WorkingUnstaged, WorkingTreeSection.Unstaged, "dir/a.txt", false);

        var target = Targets().Resolve(tab);

        Assert.Equal("dir/a.txt", target.Path);
        Assert.Null(target.Revision);
    }

    [Fact]
    public void GivenAFolderOrTheOtherListFocused_WhenResolving_ThenNothingIsBlamed()
    {
        var folder = InWorkingTree(PaneKind.WorkingUnstaged, WorkingTreeSection.Unstaged, "dir", true);
        var otherList = InWorkingTree(PaneKind.WorkingStaged, WorkingTreeSection.Unstaged, "a.txt", false);

        Assert.Equal(BlameTargets.NoFileNotice, Targets().Resolve(folder).Notice);
        Assert.Equal(BlameTargets.NoFileNotice, Targets().Resolve(otherList).Notice);
    }

    public void Dispose() => _session.Dispose();

    private static CommitFileChange Change(string path, FileChangeKind kind) => new(path, null, kind, null);

    // Row 0 is a heading, row n + 1 shows file n.
    private void ShowChanges(params CommitFileChange[] files)
    {
        List<DetailRow> rows = [new DetailRow([])];
        rows.AddRange(files.Select((_, index) => new DetailRow([], FileIndex: index)));
        _session.DetailSource.Update(new DetailContent("abc", rows, 1, files, BottomTab.Changes));
        _detail.SelectedItem = files.Length > 1 ? 2 : 1;
    }

    private RepositoryState InWorkingTree(PaneKind focused, WorkingTreeSection section, string path, bool isFolder)
    {
        var head = new HeadState(HeadKind.Branch, "main", Commit);
        var snapshot = new StatusSnapshot(new StatusBranch(head, null, null, null), [], []);

        return _tab with
        {
            View = _tab.View with { Mode = TabViewMode.WorkingTree, FocusedPane = focused },
            WorkingTree = new WorkingTreeState(snapshot, new WorkingTreeSelection(section, path, 0, isFolder)),
        };
    }

    private BlameTargets Targets() =>
        new(new FakeGraphDetailLists(new ListView(), _detail), new FakeActiveSessions(() => _session), new Selected(this));

    private sealed class Selected(BlameTargetsTests owner) : IGraphSelectionSource
    {
        public string? SelectedCommit() => owner._selectedCommit;
    }
}
