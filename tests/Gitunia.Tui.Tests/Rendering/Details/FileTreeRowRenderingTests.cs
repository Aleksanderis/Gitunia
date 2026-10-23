using Gitunia.Core.FileTree;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Trees.Models;
using Gitunia.Tui.Loading.Commits;
using Gitunia.Tui.Loading.Trees;
using Gitunia.Tui.Rendering.Details;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Sidebar;
using Gitunia.Tui.Rendering.Styling;
using Gitunia.Tui.Tests.Rendering.Graph;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Details;

public class FileTreeRowRenderingTests
{
    private const string LongName = "a-very-long-component-name-that-keeps-going-test.cs";
    private const string LongFolder = "some-quite-long-directory-name-in-the-middle";

    [Theory]
    [InlineData(30)]
    [InlineData(20)]
    [InlineData(12)]
    public void GivenALongFileNameDeepInTheTree_WhenDrawnNarrow_ThenTheNameIsCutInTheMiddleAndFitsTheWidth(int width)
    {
        var line = Draw($"a/b/{LongName}", 1, width);

        Assert.Equal(width, line.Text.Length);
        Assert.Contains("…", line.Text);
        Assert.StartsWith("   a", line.Text);
    }

    [Fact]
    public void GivenALongFolderName_WhenDrawnNarrow_ThenTheNameIsCutInTheMiddleAndTheCountOfAClosedFolderStays()
    {
        var line = Draw($"{LongFolder}/x.cs", 0, 24, closed: true);

        Assert.Equal(24, line.Text.Length);
        Assert.Contains("…", line.Text);
        Assert.EndsWith("1 file", line.Text.TrimEnd());
    }

    [Fact]
    public void GivenAFileThatFits_WhenDrawn_ThenItIsNotCut()
    {
        var line = Draw("a/b/c.cs", 1, 40);

        Assert.StartsWith("   c.cs  2.0 KB", line.Text);
        Assert.DoesNotContain("…", line.Text);
    }

    private static StyledLine Draw(string path, int row, int width, bool closed = false)
    {
        var graph = CommitFactory.Graph(CommitFactory.Commit(1, "root", []));
        var model = FileTreeModel.Build([new TreeEntry(path, TreeEntryKind.File, CommitFactory.Id(5), 2048)]);
        HashSet<string> expanded = closed ? [] : [path[..path.LastIndexOf('/')]];
        var view = new FileTreeView(
            new CommitTreeState(graph[0].Id, CommitDetailsStatus.Loaded, model, string.Empty),
            model.Rows(expanded, string.Empty),
            string.Empty,
            SidebarGlyphs.Unicode);
        var content = DetailContentBuilder.Build(graph[0], CommitDetailsState.Empty, BottomTab.Files, view);

        var drawn = content.Rows[Math.Min(row, content.Rows.Count - 1)];

        return DetailPaneRenderer.RenderRow(drawn, false, width, GraphGlyphSet.Unicode);
    }
}
