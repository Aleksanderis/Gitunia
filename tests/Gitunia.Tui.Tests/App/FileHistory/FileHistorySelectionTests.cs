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
using Gitunia.Tui.App.Selection;
using Gitunia.Tui.Rendering.Details;
using Xunit;

namespace Gitunia.Tui.Tests.App.FileHistory;

public class FileHistorySelectionTests
{
    [Fact]
    public void GivenTheChangesTab_WhenTheCursorIsOnAFileRow_ThenItsPathIsTheTarget()
    {
        var content = Changes("a.txt", "dir/b c.txt");

        Assert.Equal("a.txt", FileHistorySelection.PathOf(content, 1));
        Assert.Equal("dir/b c.txt", FileHistorySelection.PathOf(content, 2));
    }

    [Fact]
    public void GivenTheChangesTab_WhenTheCursorIsOnTheHeadingOrBeyondTheRows_ThenThereIsNoTarget()
    {
        var content = Changes("a.txt");

        Assert.Null(FileHistorySelection.PathOf(content, 0));
        Assert.Null(FileHistorySelection.PathOf(content, 9));
        Assert.Null(FileHistorySelection.PathOf(content, -1));
    }

    [Fact]
    public void GivenTheFilesTab_WhenTheCursorIsOnAFileOrADirectory_ThenOnlyTheFileIsTheTarget()
    {
        var content = new DetailContent("abc", [], 0, [], BottomTab.Files)
        {
            Tree =
            [
                new FileTreeRow(FileTreeRowKind.Directory, "src", "src", 0, true, null, 1),
                new FileTreeRow(FileTreeRowKind.File, "src/日本 ü.cs", "日本 ü.cs", 1, false, 3, 1),
            ],
        };

        Assert.Null(FileHistorySelection.PathOf(content, 0));
        Assert.Equal("src/日本 ü.cs", FileHistorySelection.PathOf(content, 1));
        Assert.Null(FileHistorySelection.PathOf(content, 2));
    }

    [Fact]
    public void GivenAFileSelectedInTheUnstagedList_WhenResolving_ThenTheHistoryStartsAtHead()
    {
        var tab = Tab(new WorkingTreeSelection(WorkingTreeSection.Unstaged, "x y.txt", 0));

        var target = FileHistorySelection.OfWorkingTree(tab, PaneKind.WorkingUnstaged);

        Assert.Equal("x y.txt", target!.Path);
        Assert.Equal(FileHistorySelection.HeadRevision, target.Revision);
    }

    [Fact]
    public void GivenAFolderOrTheOtherListsFile_WhenResolving_ThenThereIsNoTarget()
    {
        var folder = Tab(new WorkingTreeSelection(WorkingTreeSection.Unstaged, "dir", 0, true));
        var staged = Tab(new WorkingTreeSelection(WorkingTreeSection.Staged, "a.txt", 0));

        Assert.Null(FileHistorySelection.OfWorkingTree(folder, PaneKind.WorkingUnstaged));
        Assert.Null(FileHistorySelection.OfWorkingTree(staged, PaneKind.WorkingUnstaged));
        Assert.Equal("a.txt", FileHistorySelection.OfWorkingTree(staged, PaneKind.WorkingStaged)!.Path);
    }

    [Fact]
    public void GivenNoWorkingTreeLoaded_WhenResolving_ThenThereIsNoTarget()
    {
        Assert.Null(FileHistorySelection.OfWorkingTree(RepositoryStateOf(null), PaneKind.WorkingUnstaged));
    }

    private static DetailContent Changes(params string[] paths)
    {
        List<DetailRow> rows = [new DetailRow([])];
        rows.AddRange(paths.Select((_, index) => new DetailRow([], FileIndex: index)));
        var files = paths.Select(path => new CommitFileChange(path, null, FileChangeKind.Modified, null)).ToList();

        return new DetailContent("abc", rows, 1, files, BottomTab.Changes);
    }

    private static RepositoryState Tab(WorkingTreeSelection selection)
    {
        var branch = new StatusBranch(new HeadState(HeadKind.Branch, "main", new string('a', 40)), null, null, null);

        return RepositoryStateOf(new WorkingTreeState(new StatusSnapshot(branch, [], []), selection));
    }

    private static RepositoryState RepositoryStateOf(WorkingTreeState? tree)
    {
        var root = Path.Combine(Path.GetTempPath(), "gitunia-file-history-selection");
        var gitDirectory = Path.Combine(root, ".git");
        var location = new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);
        var app = AppReducer.Reduce(AppState.Empty, new OpenRepositoryCommand(location)).State;

        return app.Workspace.ActiveTab! with { WorkingTree = tree };
    }
}
