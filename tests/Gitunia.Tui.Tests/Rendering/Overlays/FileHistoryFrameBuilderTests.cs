using Gitunia.Core.Diff;
using Gitunia.Core.FileHistory;
using Gitunia.Core.Mouse;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.History.Models;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Overlays;
using Gitunia.Tui.Rendering.Styling;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Overlays;

public class FileHistoryFrameBuilderTests
{
    [Fact]
    public void GivenCommitsAndADiff_WhenRenderingAt100x24_ThenTheListIsLeftTheDiffRightAndTheFooterShowsThePosition()
    {
        var state = State(3) with { Diff = Diff("+added line", "-removed line") };

        var frame = FileHistoryFrameBuilder.Build(state, 100, 24, GraphGlyphSet.Unicode);

        Assert.NotNull(frame);
        var lines = frame.Lines.Select(line => line.Text).ToList();
        Assert.Contains("File history: dir/a b.txt (from HEAD)", lines[0], StringComparison.Ordinal);
        Assert.Contains("aaaaaaa 2026-11-02 commit 0", lines[1], StringComparison.Ordinal);
        Assert.Contains("│ ", lines[1], StringComparison.Ordinal);
        Assert.Contains("added line", lines[1], StringComparison.Ordinal);
        Assert.Contains("removed line", lines[2], StringComparison.Ordinal);
        Assert.Contains("1/3", lines[^2], StringComparison.Ordinal);
        Assert.Contains("select the commit in the graph", lines[^3], StringComparison.Ordinal);
        Assert.All(frame.Lines, line => Assert.Equal(frame.Width, TextWidth.Of(line.Text)));
    }

    [Fact]
    public void GivenTheSelectedCommit_WhenRendering_ThenOnlyItsListCellCarriesTheCursorRole()
    {
        var state = State(3).MoveTo(1);

        var frame = FileHistoryFrameBuilder.Build(state, 100, 24, GraphGlyphSet.Unicode);

        var cursorLines = frame!.Lines
            .Where(line => line.Segments.Any(segment => segment.Role == SegmentRole.Cursor))
            .ToList();
        var line = Assert.Single(cursorLines);
        Assert.Contains("bbbbbbb", line.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenARenamedPath_WhenRendering_ThenTheTitleNamesTheShortRevision()
    {
        var state = State(1) with { Revision = new string('c', 40) };

        var frame = FileHistoryFrameBuilder.Build(state, 100, 24, GraphGlyphSet.Unicode);

        Assert.Contains("(from ccccccc)", frame!.Lines[0].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenNoCommits_WhenRendering_ThenItSaysTheFileHasNoHistory()
    {
        var frame = FileHistoryFrameBuilder.Build(State(0), 100, 24, GraphGlyphSet.Unicode);

        Assert.Contains("No history", frame!.Lines[1].Text, StringComparison.Ordinal);
        Assert.Contains("0/0", frame.Lines[^2].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenADiffStillLoadingOrFailed_WhenRendering_ThenTheRightCellSaysSo()
    {
        var loading = FileHistoryFrameBuilder.Build(State(2), 100, 24, GraphGlyphSet.Unicode);
        var failed = FileHistoryFrameBuilder.Build(
            State(2) with { DiffFailure = "Could not read the diff" },
            100,
            24,
            GraphGlyphSet.Unicode);

        Assert.Contains("Loading...", loading!.Lines[1].Text, StringComparison.Ordinal);
        Assert.Contains("Could not read the diff", failed!.Lines[1].Text, StringComparison.Ordinal);
        Assert.Contains(failed.Lines[1].Segments, segment => segment.Role == SegmentRole.Error);
    }

    [Fact]
    public void GivenWideCharactersInTheSubjectAndTheDiff_WhenRendering_ThenEveryLineKeepsTheFrameWidth()
    {
        var entry = Entry(0) with { Subject = "日本語の件名がとても長いコミットメッセージです", Path = "日本 ü.txt" };
        var state = new FileHistoryState(new TabId(1), "日本 ü.txt", "HEAD", [entry], 0, Diff("+日本語のとても長い行です日本語"));

        foreach (var width in new[] { 50, 80, 120 })
        {
            var frame = FileHistoryFrameBuilder.Build(state, width, 24, GraphGlyphSet.Unicode);

            Assert.All(frame!.Lines, line => Assert.Equal(frame.Width, TextWidth.Of(line.Text)));
        }
    }

    [Fact]
    public void GivenATabInTheDiffText_WhenRendering_ThenItIsReplacedSoColumnsStay()
    {
        var state = State(1) with { Diff = Diff("+a\tb") };

        var frame = FileHistoryFrameBuilder.Build(state, 100, 24, GraphGlyphSet.Unicode);

        Assert.DoesNotContain('\t', frame!.Lines[1].Text);
    }

    [Fact]
    public void GivenAnAsciiTerminal_WhenRendering_ThenTheFrameUsesAsciiGlyphs()
    {
        var frame = FileHistoryFrameBuilder.Build(State(2) with { Diff = Diff("+x") }, 100, 24, GraphGlyphSet.Ascii);

        Assert.All(frame!.Lines, line => Assert.True(line.Text.All(character => character < 128)));
    }

    [Theory]
    [InlineData(20, 24)]
    [InlineData(100, 6)]
    [InlineData(100, 3)]
    public void GivenATinyScreen_WhenRendering_ThenNoFrameIsDrawnAndNothingThrows(int width, int height)
    {
        Assert.Null(FileHistoryFrameBuilder.Build(State(5), width, height, GraphGlyphSet.Unicode));
    }

    [Fact]
    public void GivenMoreCommitsThanRows_WhenTheSelectionIsFarDown_ThenTheWindowScrollsToIt()
    {
        var state = State(100).MoveTo(60);

        var frame = FileHistoryFrameBuilder.Build(state, 100, 12, GraphGlyphSet.Unicode);

        Assert.Equal(6, FileHistoryFrameBuilder.VisibleRows(12));
        var selectedLine = Array.FindIndex([.. frame!.Lines], line => line.Text.Contains("commit 60", StringComparison.Ordinal));
        Assert.True(selectedLine > 0);
        Assert.Equal(60, FileHistoryFrameBuilder.EntryIndexAt(state, selectedLine, 12));
    }

    [Fact]
    public void GivenTheFrameLines_WhenAskingWhatIsAtThem_ThenOnlyRowsAndTheActionRowAnswer()
    {
        var state = State(2);
        var rows = FileHistoryFrameBuilder.VisibleRows(24);

        Assert.Null(FileHistoryFrameBuilder.EntryIndexAt(state, 0, 24));
        Assert.Equal(0, FileHistoryFrameBuilder.EntryIndexAt(state, 1, 24));
        Assert.Equal(1, FileHistoryFrameBuilder.EntryIndexAt(state, 2, 24));
        Assert.Null(FileHistoryFrameBuilder.EntryIndexAt(state, 3, 24));
        Assert.Null(FileHistoryFrameBuilder.ActionAt(rows, 24));
        Assert.Equal(DefaultMouseMap.FileHistorySelectButton, FileHistoryFrameBuilder.ActionAt(rows + 1, 24));
        Assert.Null(FileHistoryFrameBuilder.ActionAt(rows + 2, 24));
    }

    private static FileHistoryState State(int count) =>
        new(new TabId(1), "dir/a b.txt", "HEAD", [.. Enumerable.Range(0, count).Select(Entry)]);

    private static FileHistoryEntry Entry(int index) =>
        new(new string((char)('a' + (index % 20)), 40), "Ann", "2026-11-02", $"commit {index}", "dir/a b.txt", null, FileChangeKind.Modified);

    private static DiffDocument Diff(params string[] lines)
    {
        var rows = lines
            .Select(line => new DiffRow(
                line[0] == '+' ? DiffRowKind.Added : DiffRowKind.Removed,
                line[1..],
                null,
                null,
                0))
            .ToList();

        return new DiffDocument(rows, [], [], [], rows.Count);
    }
}
