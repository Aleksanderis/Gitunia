using Gitunia.Core.Diff;
using Gitunia.Tui.Rendering.Diff;
using Gitunia.Tui.Rendering.Graph;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Diff;

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

    [Theory]
    [InlineData(10)]
    [InlineData(19)]
    [InlineData(29)]
    [InlineData(80)]
    public void GivenMixedRows_WhenResizing_ThenTheMapEqualsAFreshBuild(int width)
    {
        var document = Document(
            new DiffRow(DiffRowKind.Context, "", 1, 1, 0),
            new DiffRow(DiffRowKind.Added, new string('x', 100), null, 2, 0),
            new DiffRow(DiffRowKind.Added, "日本語日本語日本語", null, 3, 0),
            new DiffRow(DiffRowKind.Added, "\tindented\ttabs", null, 4, 0),
            new DiffRow(DiffRowKind.Removed, new string('y', 41), 5, null, 0));

        var resized = DiffWrapMap.Build(document, 40).Resize(width);
        var fresh = DiffWrapMap.Build(document, width);

        Assert.Equal(width, resized.Width);
        Assert.Equal(fresh.Count, resized.Count);
        Assert.Equal(Enumerable.Range(0, 6).Select(fresh.StartOf), Enumerable.Range(0, 6).Select(resized.StartOf));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(19)]
    [InlineData(80)]
    public void GivenEveryRowKindAndBoundaryLengths_WhenResizing_ThenLinesMatchTheRendererCount(int width)
    {
        var kinds = new[] { DiffRowKind.Context, DiffRowKind.Added, DiffRowKind.Removed, DiffRowKind.Warning, DiffRowKind.Note, DiffRowKind.NoNewline, DiffRowKind.FileHeader, DiffRowKind.HunkHeader };
        var rows = new List<DiffRow>();
        foreach (var kind in kinds)
        {
            foreach (var length in new[] { 0, 1, 9, 10, 11, 20, 21, 40, 41 })
            {
                rows.Add(new DiffRow(kind, new string('z', length), 1, 1, 0));
            }
        }

        var document = Document([.. rows]);
        var map = DiffWrapMap.Build(document, 33).Resize(width);
        var digits = DiffRowRenderer.GutterDigits(document.MaximumLineNumber);

        var line = 0;
        for (var index = 0; index < rows.Count; index++)
        {
            var expected = DiffRowRenderer.SegmentCount(rows[index], width, digits);
            Assert.Equal(line, map.StartOf(index));
            Assert.Equal(index, map.RowOf(line));
            Assert.Equal(index, map.RowOf(line + expected - 1));
            line += expected;
        }

        Assert.Equal(line, map.Count);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(12)]
    [InlineData(19)]
    [InlineData(31)]
    [InlineData(80)]
    public void GivenTabbedAccentedAndWideRows_WhenResizing_ThenLinesMatchTheRendererCount(int width)
    {
        var texts = new List<string>();
        foreach (var length in new[] { 0, 1, 5, 6, 7, 8, 9, 20, 40, 41 })
        {
            texts.Add(new string('\t', length / 4) + new string('a', length % 4));
            texts.Add(new string('\u00e9', length));
            texts.Add("\t" + new string('\u00fc', length) + "\t");
            texts.Add(new string('\u65e5', length));
            texts.Add("e\u0301" + new string('x', length));
            texts.Add("a\u0001b" + new string('\t', length % 3));
        }

        var kinds = new[] { DiffRowKind.Added, DiffRowKind.HunkHeader, DiffRowKind.Note };
        var rows = texts.Select((text, index) => new DiffRow(kinds[index % kinds.Length], text, null, 1, 0)).ToArray();
        var document = Document(rows);
        var map = DiffWrapMap.Build(document, 33).Resize(width);
        var digits = DiffRowRenderer.GutterDigits(document.MaximumLineNumber);

        var line = 0;
        for (var index = 0; index < rows.Length; index++)
        {
            Assert.Equal(line, map.StartOf(index));
            line += DiffRowRenderer.SegmentCount(rows[index], width, digits);
        }

        Assert.Equal(line, map.Count);
    }

    private static DiffDocument Document(params DiffRow[] rows) => new(rows, [], [0], [["f"]], 9);
}
