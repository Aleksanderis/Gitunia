using Gitunia.Core.Graph;
using Gitunia.Git;
using Gitunia.Tui.Loading;
using Gitunia.Tui.Rendering;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering;

public class DetailPaneComposerTests
{
    private static ScreenModel Model() => new("repo", "main", 1, 1, "NORMAL", string.Empty, 1, 2, false, string.Empty);

    private static CommitGraph TwoCommits() => CommitFactory.Graph(
        CommitFactory.Commit(2, "second", [1]),
        CommitFactory.Commit(1, "root", []));

    private static DetailContent LoadedContent(CommitGraph graph)
    {
        var details = new CommitDetails(
            CommitFactory.Id(2),
            [CommitFactory.Id(1)],
            "Ann",
            "ann@x.org",
            new DateTimeOffset(2026, 1, 2, 3, 4, 0, TimeSpan.Zero),
            "Ann",
            "ann@x.org",
            new DateTimeOffset(2026, 1, 2, 3, 4, 0, TimeSpan.Zero),
            "second\n\nwith a body",
            [new CommitFileChange("a.txt", null, FileChangeKind.Added, null), new CommitFileChange("b.txt", null, FileChangeKind.Modified, null)]);
        return DetailContentBuilder.Build(graph[0], new CommitDetailsState(graph[0].Id, CommitDetailsStatus.Loaded, details, string.Empty));
    }

    private static IReadOnlyList<StyledLine> Compose(DetailPaneModel? detail, int width, int height) =>
        ScreenComposer.Compose(Model(), TwoCommits(), 0, 0, width, height, GraphGlyphSet.Unicode, detail);

    [Fact]
    public void GivenLoadedDetails_WhenComposingAt50x20_ThenPaneSitsBetweenGraphAndStatus()
    {
        var graph = TwoCommits();
        var frame = string.Join('\n', Compose(new DetailPaneModel(LoadedContent(graph), 0, 0, false), 50, 20).Select(line => line.Text.TrimEnd()));

        Assert.Equal(
            string.Join(
                '\n',
                " ● Gitunia  ─  repo",
                " 1 repo  main",
                "●  0000002 second",
                "●  0000001 root",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                " Details  0000002  2 files",
                "commit    0000002000000000000000000000000000000000",
                "author    Ann <ann@x.org>  2026-01-02 03:04 +00:00",
                "parents   0000001",
                "",
                "  second",
                " NORMAL  1/2  j/k gg G ^d ^u @ q"),
            frame);
    }

    [Fact]
    public void GivenScrolledAndFocusedPane_WhenComposing_ThenRowsStartAtTheTopRowAndOnlyTheCursorRowIsSelected()
    {
        var graph = TwoCommits();
        var content = LoadedContent(graph);

        var lines = Compose(new DetailPaneModel(content, content.FirstFileRow, content.FirstFileRow - 3, true), 50, 20);

        var pane = lines.Skip(lines.Count - 6).Take(5).ToArray();
        Assert.StartsWith("  with a body", pane[0].Text, StringComparison.Ordinal);
        Assert.Equal("", pane[1].Text.Trim());
        Assert.StartsWith("2 files changed", pane[2].Text, StringComparison.Ordinal);
        Assert.StartsWith("  A a.txt", pane[3].Text, StringComparison.Ordinal);
        Assert.Equal([false, false, false, true, false], pane.Select(line => line.IsSelected));
    }

    [Fact]
    public void GivenUnfocusedPane_WhenComposing_ThenNoRowIsSelectedInIt()
    {
        var graph = TwoCommits();

        var lines = Compose(new DetailPaneModel(LoadedContent(graph), 2, 0, false), 50, 20);

        Assert.DoesNotContain(lines.Skip(lines.Count - 6), line => line.IsSelected);
    }

    [Fact]
    public void GivenFocusedPane_WhenComposing_ThenTitleIsMarkedAsActive()
    {
        var graph = TwoCommits();

        var focused = Compose(new DetailPaneModel(LoadedContent(graph), 0, 0, true), 50, 20)[^7];
        var unfocused = Compose(new DetailPaneModel(LoadedContent(graph), 0, 0, false), 50, 20)[^7];

        Assert.Equal(SegmentRole.ActiveTab, focused.Segments[0].Role);
        Assert.Equal(SegmentRole.InactiveTab, unfocused.Segments[0].Role);
    }

    [Fact]
    public void GivenShortScreen_WhenComposing_ThenThePaneIsLeftOutAndTheGraphKeepsTheRows()
    {
        var graph = TwoCommits();

        var withPane = Compose(new DetailPaneModel(LoadedContent(graph), 0, 0, false), 50, 12);
        var without = Compose(null, 50, 12);

        Assert.Equal(without.Select(line => line.Text), withPane.Select(line => line.Text));
    }

    [Theory]
    [InlineData(20, 14)]
    [InlineData(80, 24)]
    [InlineData(200, 60)]
    [InlineData(10, 15)]
    public void GivenAnyTerminalSize_WhenComposingWithPane_ThenEveryLineFillsTheWidthExactly(int width, int height)
    {
        var graph = TwoCommits();

        var lines = Compose(new DetailPaneModel(LoadedContent(graph), 0, 0, true), width, height);

        Assert.Equal(height, lines.Count);
        Assert.All(lines, line => Assert.Equal(width, TextWidth.Of(line.Text)));
    }

    [Theory]
    [InlineData(13, 0)]
    [InlineData(14, 6)]
    [InlineData(24, 8)]
    [InlineData(60, 16)]
    public void GivenScreenHeight_WhenAskingForPaneHeight_ThenItIsBoundedAndHiddenOnShortScreens(int screenHeight, int expected)
    {
        Assert.Equal(expected, DetailLayout.PaneHeight(screenHeight));
    }
}
