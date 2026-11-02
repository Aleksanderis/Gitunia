using Gitunia.App.Loading.Commits;
using Gitunia.App.Loading.Trees;
using Gitunia.Core.FileTree;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Trees.Models;
using Gitunia.Tui.Rendering.Details;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Sidebar;
using Gitunia.Tui.Tests.Rendering.Graph;
using Gitunia.Tui.Views.Lists;
using Xunit;

namespace Gitunia.Tui.Tests.Views.Lists;

[Trait("Os", "Independent")]
public class DetailListSourceRowMenuTests
{
    [Fact]
    public void GivenTheChangesTab_WhenAskingAboutRows_ThenOnlyFileRowsHaveAMenu()
    {
        var source = ChangesSource("a.txt", "b.txt");
        var fileRows = Enumerable.Range(0, source.Count).Where(source.HasRowMenu).ToList();

        Assert.Equal(2, fileRows.Count);
        Assert.All(fileRows, row => Assert.NotNull(source.Content.FilePathAt(row)));
        Assert.False(source.HasRowMenu(-1));
        Assert.False(source.HasRowMenu(source.Count));
    }

    [Fact]
    public void GivenTheFilesTab_WhenAskingAboutRows_ThenADirectoryRowHasNoMenuButItsFileHas()
    {
        var source = TreeSource("dir/a.txt", "top.txt");

        Assert.False(source.HasRowMenu(0));
        Assert.True(source.HasRowMenu(1));
        Assert.Equal("top.txt", source.Content.FilePathAt(1));
    }

    private static DetailListSource ChangesSource(params string[] paths)
    {
        var commit = CommitFactory.Commit(2, "Fix the thing", [1], []);
        var entry = CommitFactory.Graph(commit, CommitFactory.Commit(1, "root", []))[0];
        var when = new DateTimeOffset(2026, 1, 2, 3, 4, 0, TimeSpan.Zero);
        var files = paths.Select(path => new CommitFileChange(path, null, FileChangeKind.Modified, null)).ToArray();
        var details = new CommitDetails(
            CommitFactory.Id(2),
            [CommitFactory.Id(1)],
            "Ann",
            "ann@x.org",
            when,
            "Ann",
            "ann@x.org",
            when,
            "Fix the thing",
            files);
        var state = new CommitDetailsState(entry.Id, CommitDetailsStatus.Loaded, details, string.Empty);
        var source = new DetailListSource(GraphGlyphSet.Unicode);
        source.Update(DetailContentBuilder.Build(entry, state, BottomTab.Changes, null, FilePathStyle.NameFirst));

        return source;
    }

    private static DetailListSource TreeSource(params string[] paths)
    {
        var graph = CommitFactory.Graph(
            CommitFactory.Commit(2, "Fix the thing", [1], []),
            CommitFactory.Commit(1, "root", []));
        var model = FileTreeModel.Build(
            [.. paths.Select(path => new TreeEntry(path, TreeEntryKind.File, CommitFactory.Id(3), 1))]);
        var view = new FileTreeView(
            new CommitTreeState(graph[0].Id, CommitDetailsStatus.Loaded, model, string.Empty),
            model.Rows(new HashSet<string>(), string.Empty),
            string.Empty,
            SidebarGlyphs.Unicode);
        var source = new DetailListSource(GraphGlyphSet.Unicode);
        source.Update(DetailContentBuilder.Build(graph[0], CommitDetailsState.Empty, BottomTab.Files, view));

        return source;
    }
}
