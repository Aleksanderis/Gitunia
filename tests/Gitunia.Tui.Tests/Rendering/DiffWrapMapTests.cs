using Gitunia.Core.Diff;
using Gitunia.Tui.Rendering;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering;

public class DiffWrapMapTests
{
    private static readonly GraphGlyphSet Glyphs = GraphGlyphSet.Unicode;

    [Fact]
    public void GivenALongRow_WhenBuilding_ThenItTakesSeveralLinesAndLaterRowsShift()
    {
        var document = Document(
            new DiffRow(DiffRowKind.Context, "short", 1, 1, 0),
            new DiffRow(DiffRowKind.Added, new string('x', 100), null, 2, 0),
            new DiffRow(DiffRowKind.Context, "tail", 3, 3, 0));

        var map = DiffWrapMap.Build(document, 29);

        // The prefix is 9 columns, so 20 columns of text per line: 100 characters need 5 lines.
        Assert.Equal(7, map.Count);
        Assert.Equal([0, 1, 6], [map.StartOf(0), map.StartOf(1), map.StartOf(2)]);
        Assert.Equal([0, 1, 1, 1, 1, 1, 2], Enumerable.Range(0, 7).Select(map.RowOf));
    }

    [Fact]
    public void GivenAWrappedRow_WhenRendering_ThenOnlyTheFirstLineHasNumbersAndNothingIsLost()
    {
        var row = new DiffRow(DiffRowKind.Added, "abcdefghijklmnopqrstuvwxyz", null, 7, 0);
        var count = DiffRowRenderer.SegmentCount(row, 19, 3);

        var lines = Enumerable.Range(0, count).Select(segment => DiffRowRenderer.RenderSegment(row, segment, 19, 3, null, false, Glyphs)).ToList();

        Assert.Equal(3, count);
        var texts = lines.Select(line => string.Concat(line.Segments.Select(segment => segment.Text))).ToList();
        Assert.StartsWith("      7 +abcdefghij", texts[0], StringComparison.Ordinal);
        Assert.StartsWith("         klmnopqrst", texts[1], StringComparison.Ordinal);
        Assert.Equal("abcdefghijklmnopqrstuvwxyz", string.Concat(texts.Select(text => text[9..].TrimEnd())));
    }

    [Fact]
    public void GivenWideCharacters_WhenChunking_ThenNoCharacterIsSplit()
    {
        var chunks = DiffRowRenderer.Chunks("日本語日本語", 5);

        Assert.Equal(["日本", "語日", "本語"], chunks);
    }

    [Fact]
    public void GivenATabHeavyRow_WhenCountingSegments_ThenEveryColumnOfTheExpandedTextIsReachable()
    {
        var row = new DiffRow(DiffRowKind.Added, new string('\t', 7) + "abcdefghijk", null, 1, 0);
        var count = DiffRowRenderer.SegmentCount(row, 45, 3);

        var text = string.Concat(Enumerable.Range(0, count).Select(segment =>
            string.Concat(DiffRowRenderer.RenderSegment(row, segment, 45, 3, null, false, Glyphs).Segments.Select(part => part.Text))[9..].TrimEnd()));

        Assert.Equal(2, count);
        Assert.EndsWith("abcdefghijk", text, StringComparison.Ordinal);
    }

    private static DiffDocument Document(params DiffRow[] rows) => new(rows, [], [0], [["f"]], 9);
}
