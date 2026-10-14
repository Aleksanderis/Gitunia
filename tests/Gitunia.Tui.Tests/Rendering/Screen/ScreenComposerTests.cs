using Gitunia.Core.Graph;
using Gitunia.Core.Status;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Screen;
using Gitunia.Tui.Rendering.Styling;
using Gitunia.Tui.Tests.Rendering.Graph;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Screen;

public class ScreenComposerTests
{
    private static ScreenModel Model(int position, int loaded, bool isLoading = false, string pending = "", string message = "") =>
        new("repo", "main", 1, 1, "NORMAL", pending, position, loaded, isLoading, message);

    private static string Frame(ScreenModel model, CommitGraph graph, int selectedItem, int topItem, int width, int height, GraphGlyphSet? glyphs = null) =>
        string.Join('\n', ScreenComposer.Compose(model, graph, selectedItem, topItem, width, height, glyphs ?? GraphGlyphSet.Unicode).Select(line => line.Text.TrimEnd()));

    private static CommitGraph Diamond() => CommitFactory.Graph(
        CommitFactory.Commit(4, "merge side", [2, 3]),
        CommitFactory.Commit(2, "main work", [1]),
        CommitFactory.Commit(3, "side work", [1]),
        CommitFactory.Commit(1, "root", []));

    [Fact]
    public void GivenSmallGraph_WhenComposingAt40x8_ThenFrameHasTabsAFramedGraphAndStatus()
    {
        var frame = Frame(Model(1, 4), Diamond(), 0, 0, 40, 8);

        Assert.Equal(
            string.Join(
                '\n',
                "   1 repo",
                "╭─ History ────────────────────────────╮",
                "│ ○─╮  merge side                      │",
                "│ ● │  main work                       │",
                "│ ├─●  side work                       │",
                "│ ●  root                              │",
                "╰──────────────────────────────────────╯",
                " NORMAL  1/4  j/k gg G ^d ^u @ ? : q"),
            frame);
    }

    [Fact]
    public void GivenSmallGraph_WhenComposingWithoutUnicode_ThenFrameIsAscii()
    {
        var frame = Frame(Model(1, 4), Diamond(), 0, 0, 40, 8, GraphGlyphSet.Ascii);

        Assert.Equal(
            string.Join(
                '\n',
                "   1 repo",
                "+- History ----------------------------+",
                "| o-\\  merge side                      |",
                "| * |  main work                       |",
                "| |-*  side work                       |",
                "| *  root                              |",
                "+--------------------------------------+",
                " NORMAL  1/4  j/k gg G ^d ^u @ ? : q"),
            frame);
    }

    [Fact]
    public void GivenScrolledList_WhenComposing_ThenRowsStartAtTheTopItemAndSelectionIsMarked()
    {
        var graph = Diamond();

        var lines = ScreenComposer.Compose(Model(3, 4), graph, 2, 1, 40, 8, GraphGlyphSet.Unicode);

        Assert.StartsWith("│ ● │  ", lines[2].Text, StringComparison.Ordinal);
        Assert.StartsWith("│ ├─●  ", lines[3].Text, StringComparison.Ordinal);
        Assert.Equal([false, true, false], lines.Skip(2).Take(3).Select(line => line.Segments.Any(segment => segment.Selection == SegmentSelection.Focused)).ToArray());
    }

    [Fact]
    public void GivenGraphShorterThanScreen_WhenComposing_ThenRestOfThePaneIsBlankAndUnselected()
    {
        var lines = ScreenComposer.Compose(Model(1, 4), Diamond(), 0, 0, 30, 12, GraphGlyphSet.Unicode);

        Assert.Equal(12, lines.Count);
        Assert.All(lines.Skip(6).Take(4), line => Assert.Equal($"│{new string(' ', 28)}│", line.Text));
        Assert.All(lines.Skip(6).Take(4), line => Assert.DoesNotContain(line.Segments, segment => segment.Selection is SegmentSelection.Focused or SegmentSelection.Dim));
    }

    [Fact]
    public void GivenStillLoadingWithPendingChord_WhenComposing_ThenStatusShowsProgressAndChord()
    {
        var frame = Frame(Model(12, 3200, isLoading: true, pending: "5g"), Diamond(), 0, 0, 60, 4);

        Assert.EndsWith(" NORMAL  5g 12/3200+ loading...  j/k gg G ^d ^u @ ? : q", frame, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenMessage_WhenComposing_ThenStatusShowsIt()
    {
        var frame = Frame(Model(1, 4, message: "HEAD is not loaded yet"), Diamond(), 0, 0, 80, 4);

        Assert.Contains("HEAD is not loaded yet", frame, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenErrorMessage_WhenComposing_ThenStatusLineShowsItBeforeTheHintsInTheErrorRole()
    {
        var model = Model(1, 4, message: "Could not read the stashes: boom") with { MessageSeverity = StatusSeverity.Error };

        var lines = ScreenComposer.Compose(model, Diamond(), 0, 0, 80, 4, GraphGlyphSet.Unicode);

        var status = lines[^1];
        Assert.Equal(" NORMAL  1/4  Could not read the stashes: boom  j/k gg G ^d ^u @ ? : q", status.Text.TrimEnd());
        Assert.Contains(status.Segments, segment => segment.Role == SegmentRole.StatusError && segment.Text.Contains("stashes", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenEmptyGraph_WhenComposing_ThenFrameIsStillCompleteAndNothingIsSelected()
    {
        var lines = ScreenComposer.Compose(Model(0, 0, isLoading: true), new CommitGraph(), 0, 0, 40, 6, GraphGlyphSet.Unicode);

        Assert.Equal(6, lines.Count);
        Assert.All(lines, line => Assert.Equal(40, TextWidth.Of(line.Text)));
        Assert.DoesNotContain(lines, line => line.Segments.Any(segment => segment.Selection is SegmentSelection.Focused or SegmentSelection.Dim));
        Assert.Contains("0/0+ loading...", lines[^1].Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(20, 5)]
    [InlineData(80, 24)]
    [InlineData(200, 60)]
    [InlineData(10, 3)]
    [InlineData(5, 1)]
    public void GivenAnyTerminalSize_WhenComposing_ThenEveryLineFillsTheWidthExactly(int width, int height)
    {
        var lines = ScreenComposer.Compose(Model(1, 4), Diamond(), 0, 0, width, height, GraphGlyphSet.Unicode);

        Assert.Equal(height, lines.Count);
        Assert.All(lines, line => Assert.Equal(width, TextWidth.Of(line.Text)));
    }

    [Fact]
    public void GivenWideSubjects_WhenComposing_ThenColumnsStayAligned()
    {
        var graph = CommitFactory.Graph(
            CommitFactory.Commit(2, "修正バグ", [1]),
            CommitFactory.Commit(1, "emoji 🎉 ok", []));

        var lines = ScreenComposer.Compose(Model(1, 2), graph, 0, 0, 30, 7, GraphGlyphSet.Unicode);

        Assert.Equal("│ ●  修正バグ                │", lines[2].Text);
        Assert.Equal("│ ●  emoji 🎉 ok             │", lines[3].Text);
        Assert.All(lines, line => Assert.Equal(30, TextWidth.Of(line.Text)));
    }
}
