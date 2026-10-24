using Gitunia.Core.Diff;
using Gitunia.Tui.Rendering.Diff;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Styling;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Diff;

[Trait("Os", "Independent")]
public class DiffSplitRendererTests
{
    private const int Digits = 3;
    private static readonly GraphGlyphSet Glyphs = GraphGlyphSet.Unicode;

    [Fact]
    public void GivenAPairedLine_WhenRendering_ThenOldAndNewSideShowTheirOwnNumbersMarkersAndText()
    {
        var document = Document(
            new DiffRow(DiffRowKind.Removed, "old text", 4, null, 0),
            new DiffRow(DiffRowKind.Added, "new text", null, 4, 0));
        var line = DiffSplitLayout.Build(document).Lines[0];

        var text = DiffSplitRenderer.Render(document, line, 0, 41, Digits, null, false, false, Glyphs).Text;

        Assert.Equal("  4 -old text       │  4 +new text       ", text);
        Assert.Equal(41, TextWidth.Of(text));
    }

    [Fact]
    public void GivenWhitespaceMarksOn_WhenRenderingASplitLine_ThenBothSidesShowThemInTheSameColumns()
    {
        var document = Document(
            new DiffRow(DiffRowKind.Removed, "a\tb", 4, null, 0),
            new DiffRow(DiffRowKind.Added, "c  ", null, 4, 0));
        var line = DiffSplitLayout.Build(document).Lines[0];

        var plain = DiffSplitRenderer.Render(document, line, 0, 41, Digits, null, false, false, Glyphs).Text;
        var marked = DiffSplitRenderer.Render(
            document, line, 0, 41, Digits, null, false, false, Glyphs, showWhitespace: true).Text;

        Assert.Contains("a→  b", marked, StringComparison.Ordinal);
        Assert.Contains("c··", marked, StringComparison.Ordinal);
        Assert.Equal(TextWidth.Of(plain), TextWidth.Of(marked));
    }

    [Fact]
    public void GivenWhitespaceMarksOnAndWrap_WhenRenderingTheSecondSegment_ThenTheMarkInItShows()
    {
        var document = Document(
            new DiffRow(DiffRowKind.Removed, "abcdefghijklmno\tz", 4, null, 0),
            new DiffRow(DiffRowKind.Added, "new", null, 4, 0));
        var line = DiffSplitLayout.Build(document).Lines[0];

        var second = DiffSplitRenderer.Render(
            document, line, 1, 41, Digits, null, false, true, Glyphs, showWhitespace: true);

        Assert.Contains("→", second.Text, StringComparison.Ordinal);
        Assert.Equal(SegmentRole.Faint, second.Segments.First(segment => segment.Text.StartsWith('→')).Role);
    }

    [Fact]
    public void GivenALineWithOnlyAnAddedRow_WhenRendering_ThenTheOldSideIsBlank()
    {
        var document = Document(new DiffRow(DiffRowKind.Added, "extra", null, 9, 0));
        var line = DiffSplitLayout.Build(document).Lines[0];

        var text = DiffSplitRenderer.Render(document, line, 0, 41, Digits, null, false, false, Glyphs).Text;

        Assert.StartsWith(new string(' ', 20) + "│", text, StringComparison.Ordinal);
        Assert.Contains("  9 +extra", text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAContextLine_WhenRendering_ThenBothSidesShowItWithTheirNumbers()
    {
        var document = Document(new DiffRow(DiffRowKind.Context, "same", 7, 8, 0));
        var line = DiffSplitLayout.Build(document).Lines[0];

        var text = DiffSplitRenderer.Render(document, line, 0, 41, Digits, null, false, false, Glyphs).Text;

        Assert.Equal("  7  same           │  8  same           ", text);
    }

    [Fact]
    public void GivenAHunkHeader_WhenRendering_ThenItSpansBothSides()
    {
        var document = Document(new DiffRow(DiffRowKind.HunkHeader, "@@ -1 +1 @@", null, null, 0));
        var line = DiffSplitLayout.Build(document).Lines[0];

        var text = DiffSplitRenderer.Render(document, line, 0, 41, Digits, null, false, false, Glyphs).Text;

        Assert.StartsWith("── @@ -1 +1 @@ ─", text, StringComparison.Ordinal);
        Assert.DoesNotContain('│', text);
    }

    [Fact]
    public void GivenALongRemovedRowAndAShortAddedOne_WhenWrapped_ThenTheLineTakesTheLongerSidesLines()
    {
        var document = Document(
            new DiffRow(DiffRowKind.Removed, new string('x', 50), 1, null, 0),
            new DiffRow(DiffRowKind.Added, "short", null, 1, 0));
        var line = DiffSplitLayout.Build(document).Lines[0];

        var count = DiffSplitRenderer.SegmentCount(document, line, 41, Digits);
        var second = DiffSplitRenderer.Render(document, line, 1, 41, Digits, null, false, true, Glyphs).Text;

        Assert.Equal(4, count);
        Assert.Equal(41, TextWidth.Of(second));
        Assert.Contains('│', second);
        Assert.DoesNotContain("short", second, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(80)]
    [InlineData(81)]
    [InlineData(120)]
    public void GivenAnyWidth_WhenRendering_ThenTheLineHasExactlyThatWidthWithTheRuleInTheMiddle(int width)
    {
        var document = Document(
            new DiffRow(DiffRowKind.Removed, "abcdefg" + new string('y', 200), 1, null, 0),
            new DiffRow(DiffRowKind.Added, "tab\tseparated", null, 1, 0));
        var line = DiffSplitLayout.Build(document).Lines[0];

        var text = DiffSplitRenderer.Render(document, line, 0, width, Digits, null, true, false, Glyphs).Text;

        Assert.Equal(width, TextWidth.Of(text));
        Assert.Equal('│', text[DiffSplitRenderer.LeftWidth(width)]);
    }

    [Fact]
    public void GivenTheAsciiTier_WhenRendering_ThenTheRuleIsAPlainBar()
    {
        var document = Document(new DiffRow(DiffRowKind.Context, "same", 1, 1, 0));
        var line = DiffSplitLayout.Build(document).Lines[0];

        var text = DiffSplitRenderer.Render(document, line, 0, 41, Digits, null, false, false, GraphGlyphSet.Ascii).Text;

        Assert.All(text, character => Assert.True(character < 128));
        Assert.Contains('|', text);
    }

    private static DiffDocument Document(params DiffRow[] rows) => new(rows, [], [], [], 20);
}
