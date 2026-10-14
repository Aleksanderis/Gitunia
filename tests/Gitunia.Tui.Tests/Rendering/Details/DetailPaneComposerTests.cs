using Gitunia.Core.Graph;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Commits.Models;
using Gitunia.Tui.Loading.Commits;
using Gitunia.Tui.Rendering.Details;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Screen;
using Gitunia.Tui.Rendering.Styling;
using Gitunia.Tui.Tests.Rendering.Graph;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Details;

public class DetailPaneComposerTests
{
    private static ScreenModel Model() => new("repo", "main", 1, 1, "NORMAL", string.Empty, 1, 2, false, string.Empty);

    private static CommitGraph TwoCommits() => CommitFactory.Graph(
        CommitFactory.Commit(2, "second", [1]),
        CommitFactory.Commit(1, "root", []));

    private static DetailContent LoadedContent(CommitGraph graph, BottomTab tab = BottomTab.Details)
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
        return DetailContentBuilder.Build(graph[0], new CommitDetailsState(graph[0].Id, CommitDetailsStatus.Loaded, details, string.Empty), tab);
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
                "   1 repo",
                "╭─ History ──────────────────────────────────────╮",
                "│ ●  second                                      │",
                "│ ●  root                                        │",
                "│                                                │",
                "│                                                │",
                "│                                                │",
                "│                                                │",
                "│                                                │",
                "│                                                │",
                "│                                                │",
                "│                                                │",
                "├─ Details ─ Changes ─ Files ─ 0000002 ─ 2 files ┤",
                "│ commit    00000020000000000000000000000000000… │",
                "│ author    Ann <ann@x.org>  2026-01-02 03:04 +… │",
                "│ parents   0000001                              │",
                "│                                                │",
                "│   second                                       │",
                "╰────────────────────────────────────────────────╯",
                " NORMAL  1/2  j/k move  gg/G top/end  Ctrl-d/u pa…"),
            frame);
    }

    [Fact]
    public void GivenTheChangesTab_WhenComposingAt50x20_ThenThePaneListsTheFiles()
    {
        var graph = TwoCommits();

        var pane = PaneLines(new DetailPaneModel(LoadedContent(graph, BottomTab.Changes), 0, 0, false));

        Assert.Equal(
            [
                "─ Details ─ Changes ─ Files ─ 0000002 ─ 2 files",
                " 2 files changed",
                " A a.txt",
                " M b.txt",
                "",
                "",
            ],
            pane);
    }

    [Fact]
    public void GivenTheFilesTab_WhenComposingAt50x20_ThenThePaneShowsThePlaceholder()
    {
        var graph = TwoCommits();

        var pane = PaneLines(new DetailPaneModel(LoadedContent(graph, BottomTab.Files), 0, 0, false));

        Assert.Equal(
            [
                "─ Details ─ Changes ─ Files ─ 0000002 ─ 2 files",
                " The file tree is not implemented yet",
                "",
                "",
                "",
                "",
            ],
            pane);
    }

    [Theory]
    [InlineData(BottomTab.Details, 0)]
    [InlineData(BottomTab.Changes, 1)]
    [InlineData(BottomTab.Files, 2)]
    public void GivenAFocusedPane_WhenComposing_ThenOnlyTheSelectedTabLabelIsMarkedActive(BottomTab tab, int expectedIndex)
    {
        var graph = TwoCommits();

        var title = Compose(new DetailPaneModel(LoadedContent(graph, tab), 0, 0, true), 50, 20)[^8];

        var roles = title.Segments.Where(segment => segment.Text.Trim() is "Details" or "Changes" or "Files").Select(segment => segment.Role).ToArray();
        Assert.Equal(3, roles.Length);
        Assert.Equal(SegmentRole.ActiveTab, roles[expectedIndex]);
        Assert.Equal(2, roles.Count(role => role == SegmentRole.TabButton));
    }

    [Fact]
    public void GivenDetailsThatHaveNotArrived_WhenRenderingTheTitle_ThenTheFileCountIsAPlaceholderInsteadOfZero()
    {
        var graph = TwoCommits();
        var state = new CommitDetailsState(graph[0].Id, CommitDetailsStatus.Loading, null, string.Empty);

        var title = DetailPaneRenderer.RenderTitle(DetailContentBuilder.Build(graph[0], state, BottomTab.Changes), true, 60, GraphGlyphSet.Unicode);

        Assert.Contains(" .. files ", title.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("0 files", title.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAPaneTooNarrowForPaddedLabels_WhenRenderingTheTitle_ThenTheLabelsLoseTheirPaddingAndStillFit()
    {
        var content = LoadedContent(TwoCommits(), BottomTab.Changes);

        var title = DetailPaneRenderer.RenderTitle(content, true, 24, GraphGlyphSet.Unicode);

        Assert.Equal(24, TextWidth.Of(title.Text));
        Assert.StartsWith("─Details─Changes─Files", title.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheTabStrip_WhenLookingForTheLabelUnderAColumn_ThenItMatchesWhatTheTitleDraws()
    {
        var wide = DetailTabStrip.Layout(50);
        var narrow = DetailTabStrip.Layout(24);

        Assert.Equal([1, 11, 21], wide.Select(button => button.Start));
        Assert.Equal("DetailTab.Details", TitleButton.ViewAt(wide, 3));
        Assert.Equal("DetailTab.Changes", TitleButton.ViewAt(wide, 12));
        Assert.Null(TitleButton.ViewAt(wide, 10));
        Assert.Equal("DetailTab.Files", TitleButton.ViewAt(wide, 24));
        Assert.Null(TitleButton.ViewAt(wide, 28));
        Assert.Null(TitleButton.ViewAt(wide, 0));
        Assert.Equal([1, 9, 17], narrow.Select(button => button.Start));
        Assert.Null(TitleButton.ViewAt(narrow, 8));
        Assert.Equal("DetailTab.Changes", TitleButton.ViewAt(narrow, 12));
    }

    [Fact]
    public void GivenTheAsciiSet_WhenRenderingTheTitle_ThenEachTabIsABracketedButtonOfTheSameWidth()
    {
        var content = LoadedContent(TwoCommits(), BottomTab.Changes);

        var title = DetailPaneRenderer.RenderTitle(content, true, 50, GraphGlyphSet.Ascii);

        Assert.Equal(50, TextWidth.Of(title.Text));
        Assert.StartsWith("-[Details]-[Changes]-[Files]-", title.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenALoadedPane_WhenRendering_ThenEveryContentCellIsOnThePanelButTheTitleIsNot()
    {
        var content = LoadedContent(TwoCommits(), BottomTab.Details);

        var lines = DetailPaneRenderer.RenderPane(new DetailPaneModel(content, 0, 0, false), 40, 4, GraphGlyphSet.Unicode);

        Assert.DoesNotContain(lines[0].Segments, segment => segment.OnPanel);
        Assert.All(lines.Skip(1), line => Assert.All(line.Segments, segment => Assert.True(segment.OnPanel)));
    }

    [Fact]
    public void GivenScrolledAndFocusedPane_WhenComposing_ThenRowsStartAtTheTopRowAndOnlyTheCursorRowIsSelected()
    {
        var graph = TwoCommits();
        var content = LoadedContent(graph, BottomTab.Changes);

        var lines = Compose(new DetailPaneModel(content, content.FirstFileRow, 1, true), 50, 20);

        var pane = lines.Skip(lines.Count - 7).Take(5).ToArray();
        Assert.StartsWith("│ A a.txt", pane[0].Text, StringComparison.Ordinal);
        Assert.StartsWith("│ M b.txt", pane[1].Text, StringComparison.Ordinal);
        Assert.Equal([true, false, false, false, false], pane.Select(line => line.Segments.Any(segment => segment.Selection == SegmentSelection.Focused)));
    }

    [Fact]
    public void GivenUnfocusedPane_WhenComposing_ThenNoRowIsSelectedInIt()
    {
        var graph = TwoCommits();

        var lines = Compose(new DetailPaneModel(LoadedContent(graph), 2, 0, false), 50, 20);

        Assert.DoesNotContain(lines.Skip(lines.Count - 7), line => line.Segments.Any(segment => segment.Selection is SegmentSelection.Focused or SegmentSelection.Dim));
    }

    [Fact]
    public void GivenFocusedPane_WhenComposing_ThenTitleIsMarkedAsActive()
    {
        var graph = TwoCommits();

        var focused = Compose(new DetailPaneModel(LoadedContent(graph), 0, 0, true), 50, 20)[^8];
        var unfocused = Compose(new DetailPaneModel(LoadedContent(graph), 0, 0, false), 50, 20)[^8];

        Assert.Contains(focused.Segments, segment => segment.Role == SegmentRole.ActiveTab && segment.Text == " Details ");
        Assert.Contains(unfocused.Segments, segment => segment.Role == SegmentRole.InactiveTab && segment.Text == " Details ");
        Assert.Equal(SegmentRole.FocusedBorder, focused.Segments[0].Role);
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

    private static string[] PaneLines(DetailPaneModel detail)
    {
        var lines = Compose(detail, 50, 20);
        return [.. lines.Skip(lines.Count - 8).Take(6).Select(line => line.Text[1..^1].TrimEnd(' ', '─'))];
    }
}
