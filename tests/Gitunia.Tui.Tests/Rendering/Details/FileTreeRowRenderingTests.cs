using Gitunia.App.Loading.Commits;
using Gitunia.App.Loading.Trees;
using Gitunia.Core.FileTree;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Trees.Models;
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
    private const string DeepPath = "one/two/three";

    [Theory]
    [InlineData(40)]
    [InlineData(30)]
    [InlineData(24)]
    public void GivenALongFileNameDeepInTheTree_WhenDrawnNarrow_ThenTheNameIsCutInTheMiddleKeepingHeadTailAndSize(
        int width)
    {
        var line = Draw(DeepPath, LongName, width);

        Assert.Equal(width, line.Text.Length);
        Assert.StartsWith("   a", line.Text);
        Assert.Matches("a.*…[^…]*st\\.cs", line.Text);
        Assert.EndsWith("2.0 KB", line.Text.TrimEnd());
    }

    [Fact]
    public void GivenALongFolderName_WhenDrawnNarrow_ThenTheNameIsCutInTheMiddleAndTheCountOfAClosedFolderStays()
    {
        var line = Draw(LongFolder, "x.cs", 24, closed: true);

        Assert.Equal(24, line.Text.Length);
        Assert.Matches("some.*…", line.Text);
        Assert.EndsWith("1 file", line.Text.TrimEnd());
    }

    [Fact]
    public void GivenAFileThatFits_WhenDrawn_ThenItIsNotCut()
    {
        var line = Draw(DeepPath, "c.cs", 40);

        Assert.StartsWith("   c.cs  2.0 KB", line.Text);
        Assert.DoesNotContain("…", line.Text);
    }

    [Fact]
    public void GivenALongFileName_WhenTheQuickJumpAsksForTheRowText_ThenItIsTheCutTextOnScreen()
    {
        var content = Content(DeepPath, LongName);

        var jumpText = DetailJumpRows.TextOf(content, content.Rows.Count - 1, 30, "…");
        var drawn = DetailPaneRenderer.RenderRow(content.Rows[^1], false, 30, GraphGlyphSet.Unicode).Text;

        Assert.Equal(drawn.TrimEnd(), jumpText);
        Assert.Contains("…", jumpText);
    }

    private static DetailContent Content(string folder, string name, bool closed = false)
    {
        var graph = CommitFactory.Graph(CommitFactory.Commit(1, "root", []));
        var model = FileTreeModel.Build(
            [new TreeEntry($"{folder}/{name}", TreeEntryKind.File, CommitFactory.Id(5), 2048)]);
        HashSet<string> expanded = closed ? [] : [folder];
        var view = new FileTreeView(
            new CommitTreeState(graph[0].Id, CommitDetailsStatus.Loaded, model, string.Empty),
            model.Rows(expanded, string.Empty),
            string.Empty,
            SidebarGlyphs.Unicode);

        return DetailContentBuilder.Build(graph[0], CommitDetailsState.Empty, BottomTab.Files, view);
    }

    private static StyledLine Draw(string folder, string name, int width, bool closed = false)
    {
        var content = Content(folder, name, closed);

        return DetailPaneRenderer.RenderRow(content.Rows[^1], false, width, GraphGlyphSet.Unicode);
    }
}
