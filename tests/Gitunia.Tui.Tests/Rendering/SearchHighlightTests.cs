using Gitunia.Core.Search;
using Gitunia.Tui.Rendering;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering;

public class SearchHighlightTests
{
    private static ScreenModel Model(string searchLine = "") =>
        new("repo", "main", 1, 1, "INSERT", string.Empty, 1, 3, false, string.Empty, searchLine);

    private static Gitunia.Core.Graph.CommitGraph Graph() => CommitFactory.Graph(
        CommitFactory.Commit(3, "Fix the Parser", [2]),
        CommitFactory.Commit(2, "Add tests", [1]),
        CommitFactory.Commit(1, "fix lint", []));

    private static string Highlighted(StyledLine line) =>
        string.Concat(line.Segments.Where(segment => segment.Role == SegmentRole.SearchMatch).Select(segment => segment.Text));

    [Fact]
    public void GivenMessageQuery_WhenRendering_ThenOnlyTheMatchedTextOfMatchingRowsIsMarked()
    {
        var graph = Graph();
        var options = new GraphRowOptions(40, 1, GraphGlyphSet.Unicode, new SearchQuery(SearchField.Message, "fix"));

        Assert.Equal("Fix", Highlighted(GraphRowRenderer.Render(graph[0], options, false)));
        Assert.Equal(string.Empty, Highlighted(GraphRowRenderer.Render(graph[1], options, false)));
        Assert.Equal("fix", Highlighted(GraphRowRenderer.Render(graph[2], options, false)));
    }

    [Fact]
    public void GivenMessageQuery_WhenRendering_ThenTheTextIsUnchangedByTheHighlight()
    {
        var graph = Graph();
        var plain = new GraphRowOptions(40, 1, GraphGlyphSet.Unicode);
        var marked = plain with { Highlight = new SearchQuery(SearchField.Message, "parser") };

        Assert.Equal(GraphRowRenderer.Render(graph[0], plain, false).Text, GraphRowRenderer.Render(graph[0], marked, false).Text);
    }

    [Fact]
    public void GivenHashQuery_WhenRendering_ThenTheHashIsMarked()
    {
        var graph = Graph();
        var options = new GraphRowOptions(100, 1, GraphGlyphSet.Unicode, new SearchQuery(SearchField.Hash, "000000"));

        Assert.Equal("0000003", Highlighted(GraphRowRenderer.Render(graph[0], options, false)));
    }

    [Fact]
    public void GivenAuthorQuery_WhenRendering_ThenTheAuthorIsMarked()
    {
        var graph = Graph();
        var options = new GraphRowOptions(100, 1, GraphGlyphSet.Unicode, new SearchQuery(SearchField.Author, "auth"));

        Assert.Equal("Author", Highlighted(GraphRowRenderer.Render(graph[0], options, false)).TrimEnd());
    }

    [Fact]
    public void GivenOpenSearch_WhenComposing_ThenTheStatusShowsItInsteadOfThePositionAndHints()
    {
        var lines = ScreenComposer.Compose(Model("/fix_  2 found"), Graph(), 0, 0, 60, 6, GraphGlyphSet.Unicode, highlight: new SearchQuery(SearchField.Message, "fix"));

        Assert.Equal(" INSERT  /fix_  2 found", lines[^1].Text.TrimEnd());
        Assert.Equal("Fix", Highlighted(lines[2]));
    }
}
