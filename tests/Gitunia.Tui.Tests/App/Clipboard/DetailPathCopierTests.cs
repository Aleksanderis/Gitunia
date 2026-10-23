using System.Text;
using Gitunia.Core.Clipboard;
using Gitunia.Core.FileTree;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Trees.Models;
using Gitunia.Tui.App.Clipboard;
using Gitunia.Tui.Loading.Commits;
using Gitunia.Tui.Loading.Trees;
using Gitunia.Tui.Rendering.Details;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Sidebar;
using Gitunia.Tui.Tests.Rendering.Graph;
using Gitunia.Tui.Views.Lists;
using Xunit;

namespace Gitunia.Tui.Tests.App.Clipboard;

public class DetailPathCopierTests : IDisposable
{
    private readonly StringWriter _terminal = new();

    [Fact]
    public void GivenAFileRow_WhenCopying_ThenItsPathGoesToTheClipboard()
    {
        var source = LoadedSource("src/a.cs", "docs/b.md");

        var notice = CreateCopier().Copy(source, source.Content.FirstFileRow + 1);

        Assert.Equal(string.Empty, notice);
        Assert.Equal(Osc52Sequence("docs/b.md"), _terminal.ToString());
    }

    [Fact]
    public void GivenTheTreeStyleOnChanges_WhenCopying_ThenAFolderRowCopiesTheFolderAndAFileRowTheWholePath()
    {
        var source = LoadedSource(FilePathStyle.Tree, "src/lib/a.cs");

        // Rows: heading, "src/", "lib/", "a.cs".
        var folder = CreateCopier().Copy(source, 2);
        var file = CreateCopier().Copy(source, 3);

        Assert.Equal(string.Empty, folder + file);
        Assert.Equal(Osc52Sequence("src/lib") + Osc52Sequence("src/lib/a.cs"), _terminal.ToString());
    }

    [Fact]
    public void GivenARowAboveTheFiles_WhenCopying_ThenANoticeSaysToSelectAFile()
    {
        var source = LoadedSource("src/a.cs");

        var notice = CreateCopier().Copy(source, source.Content.FirstFileRow - 1);

        Assert.Equal("Select a file to copy its path", notice);
        Assert.Equal(string.Empty, _terminal.ToString());
    }

    [Fact]
    public void GivenARowPastTheLastFile_WhenCopying_ThenNothingIsCopied()
    {
        var source = LoadedSource("src/a.cs");

        var notice = CreateCopier().Copy(source, source.Content.FirstFileRow + 1);

        Assert.Equal("Select a file to copy its path", notice);
        Assert.Equal(string.Empty, _terminal.ToString());
    }

    [Fact]
    public void GivenNoSelectionOrNoSession_WhenCopying_ThenNothingIsCopied()
    {
        var source = LoadedSource("src/a.cs");

        Assert.Equal("Select a file to copy its path", CreateCopier().Copy(source, null));
        Assert.Equal("Select a file to copy its path", CreateCopier().Copy(null, 0));
        Assert.Equal(string.Empty, _terminal.ToString());
    }

    [Fact]
    public void GivenATreeRowOnTheFilesTab_WhenCopying_ThenItsFullPathGoesToTheClipboard()
    {
        var source = TreeSource("src/lib/a.cs", "src/lib/b.cs", "top.txt");

        // Rows: "src/lib" (folded directory), "top.txt".
        var directory = CreateCopier().Copy(source, 0);
        var file = CreateCopier().Copy(source, 1);

        Assert.Equal(string.Empty, directory);
        Assert.Equal(string.Empty, file);
        Assert.Equal(Osc52Sequence("src/lib") + Osc52Sequence("top.txt"), _terminal.ToString());
    }

    [Fact]
    public void GivenARowPastTheTree_WhenCopying_ThenANoticeSaysToSelectAFile()
    {
        var source = TreeSource("a.txt");

        Assert.Equal("Select a file to copy its path", CreateCopier().Copy(source, 5));
        Assert.Equal(string.Empty, _terminal.ToString());
    }

    public void Dispose()
    {
        _terminal.Dispose();
        GC.SuppressFinalize(this);
    }

    private static DetailListSource LoadedSource(params string[] paths) =>
        LoadedSource(FilePathStyle.NameFirst, paths);

    private static DetailListSource LoadedSource(FilePathStyle style, params string[] paths)
    {
        var commit = CommitFactory.Commit(2, "Fix the thing", [1], []);
        var entry = CommitFactory.Graph(commit, CommitFactory.Commit(1, "root", []))[0];
        var when = new DateTimeOffset(2026, 1, 2, 3, 4, 0, TimeSpan.Zero);
        var files = paths.Select(path => new CommitFileChange(path, null, FileChangeKind.Modified, null)).ToArray();
        var details = new CommitDetails(CommitFactory.Id(2), [CommitFactory.Id(1)], "Ann", "ann@x.org", when, "Ann", "ann@x.org", when, "Fix the thing", files);
        var source = new DetailListSource(GraphGlyphSet.Unicode);
        var state = new CommitDetailsState(entry.Id, CommitDetailsStatus.Loaded, details, string.Empty);
        source.Update(DetailContentBuilder.Build(entry, state, BottomTab.Changes, null, style));
        return source;
    }

    private static DetailListSource TreeSource(params string[] paths)
    {
        var graph = CommitFactory.Graph(
            CommitFactory.Commit(2, "Fix the thing", [1], []),
            CommitFactory.Commit(1, "root", []));
        var entry = graph[0];
        var model = FileTreeModel.Build(
            [.. paths.Select(path => new TreeEntry(path, TreeEntryKind.File, CommitFactory.Id(3), 1))]);
        var view = new FileTreeView(
            new CommitTreeState(entry.Id, CommitDetailsStatus.Loaded, model, string.Empty),
            model.Rows(new HashSet<string>(), string.Empty),
            string.Empty,
            SidebarGlyphs.Unicode);
        var source = new DetailListSource(GraphGlyphSet.Unicode);
        source.Update(DetailContentBuilder.Build(entry, CommitDetailsState.Empty, BottomTab.Files, view));

        return source;
    }

    private static string Osc52Sequence(string text) => $"\u001b]52;c;{Convert.ToBase64String(Encoding.UTF8.GetBytes(text))}\u001b\\";

    private DetailPathCopier CreateCopier() =>
        new(new ClipboardCopier(new ClipboardWriter(_terminal, new Osc52Options(Osc52Mode.Direct, 100_000)), new StatusChannel()));
}
