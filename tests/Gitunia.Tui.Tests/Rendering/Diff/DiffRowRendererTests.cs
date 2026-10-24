using Gitunia.Core.Diff;
using Gitunia.Tui.Rendering.Diff;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Styling;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Diff;

public class DiffRowRendererTests
{
    private static readonly GraphGlyphSet Glyphs = GraphGlyphSet.Unicode;

    [Fact]
    public void GivenAnAddedLine_WhenRendering_ThenGutterMarkerAndTextFillTheWidth()
    {
        var row = new DiffRow(DiffRowKind.Added, "hello", null, 12, 0);

        var line = DiffRowRenderer.Render(row, 30, 3, null, false, Glyphs);

        Assert.StartsWith("     12 +hello", line.Text);
        Assert.Equal(30, TextWidth.Of(line.Text));
        Assert.Equal(SegmentRole.DiffAdded, line.Segments.First(segment => segment.Text == "hello").Role);
    }

    [Fact]
    public void GivenARemovedAndAContextLine_WhenRendering_ThenBothNumbersShowForContext()
    {
        var context = DiffRowRenderer.Render(new DiffRow(DiffRowKind.Context, "same", 4, 5, 0), 20, 3, null, false, Glyphs);
        var removed = DiffRowRenderer.Render(new DiffRow(DiffRowKind.Removed, "gone", 4, null, 0), 20, 3, null, false, Glyphs);

        Assert.StartsWith("  4   5  same", context.Text);
        Assert.StartsWith("  4     -gone", removed.Text);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(17)]
    public void GivenWideCharacters_WhenCutToAnyWidth_ThenTheLineNeverOverflowsOrSplitsACharacter(int width)
    {
        var row = new DiffRow(DiffRowKind.Added, "日本語のテキストと絵文字😀です", null, 1, 0);

        var line = DiffRowRenderer.Render(row, width, 3, null, false, Glyphs);

        Assert.Equal(width, TextWidth.Of(line.Text));
        Assert.DoesNotContain('�', line.Text);
    }

    [Fact]
    public void GivenAVeryLongLine_WhenRendering_ThenItIsCutToTheWidthWithAnEllipsisAndMatchesInViewStayHighlighted()
    {
        var text = $"needle{string.Concat(Enumerable.Repeat("\tx", 500_000))}";
        var row = new DiffRow(DiffRowKind.Added, text, null, 1, 0);

        var line = DiffRowRenderer.Render(row, 40, 3, "needle", false, Glyphs);

        var shown = string.Concat(line.Segments.Select(segment => segment.Text));
        Assert.Equal(40, TextWidth.Of(shown));
        Assert.EndsWith(Glyphs.Ellipsis, shown, StringComparison.Ordinal);
        Assert.Contains(line.Segments, segment => segment.Role == SegmentRole.SearchMatch && segment.Text == "needle");
    }

    [Fact]
    public void GivenALongLineEndingInASurrogatePair_WhenRendering_ThenThePairIsNeverSplit()
    {
        var text = new string('a', 31) + string.Concat(Enumerable.Repeat("\U0001F600", 100));
        var row = new DiffRow(DiffRowKind.Context, text, 1, 1, 0);

        var line = DiffRowRenderer.Render(row, 10, 3, null, false, Glyphs);

        Assert.DoesNotContain(line.Segments, segment => segment.Text.Length > 0 && (char.IsHighSurrogate(segment.Text[^1]) || char.IsLowSurrogate(segment.Text[0])));
    }

    [Fact]
    public void GivenTabsInTheText_WhenRendering_ThenTheyExpandToTabStops()
    {
        Assert.Equal("a   b       c", DiffRowRenderer.ExpandTabs("a\tb\t\tc"));
        Assert.Equal("ab  c", DiffRowRenderer.ExpandTabs("ab\tc"));
        Assert.Equal("日  x", DiffRowRenderer.ExpandTabs("日\tx"));
    }

    [Fact]
    public void GivenControlCharacters_WhenRendering_ThenTheyBecomeSpaces()
    {
        Assert.Equal("a b c", DiffRowRenderer.ExpandTabs("a\u0001b\rc"));
    }

    [Fact]
    public void GivenALongLine_WhenRendering_ThenItIsCutWithAnEllipsisNotWrapped()
    {
        var row = new DiffRow(DiffRowKind.Context, new string('x', 100), 1, 1, 0);

        var line = DiffRowRenderer.Render(row, 40, 3, null, false, Glyphs);

        Assert.Equal(40, TextWidth.Of(line.Text));
        Assert.EndsWith("…", line.Text);
    }

    [Fact]
    public void GivenAHighlight_WhenRendering_ThenMatchesGetTheSearchRoleIgnoringCase()
    {
        var row = new DiffRow(DiffRowKind.Context, "Foo bar FOO", 1, 1, 0);

        var line = DiffRowRenderer.Render(row, 40, 3, "foo", false, Glyphs);

        var matches = line.Segments.Where(segment => segment.Role == SegmentRole.SearchMatch).Select(segment => segment.Text).ToArray();
        Assert.Equal(["Foo", "FOO"], matches);
    }

    [Fact]
    public void GivenAHunkHeader_WhenRendering_ThenItIsARuleSeparatorAcrossTheWholeWidth()
    {
        var row = new DiffRow(DiffRowKind.HunkHeader, "@@ -1 +1 @@ Foo", null, null, 0);

        var line = DiffRowRenderer.Render(row, 30, 3, null, false, Glyphs);

        Assert.Equal("── @@ -1 +1 @@ Foo ───────────", line.Text);
        Assert.Equal(SegmentRole.Faint, line.Segments[0].Role);
        Assert.Equal(SegmentRole.Faint, line.Segments[^1].Role);
        Assert.Equal(30, TextWidth.Of(line.Text));
    }

    [Fact]
    public void GivenAHunkHeaderInTheAsciiSet_WhenRendering_ThenTheSeparatorUsesPlainDashes()
    {
        var row = new DiffRow(DiffRowKind.HunkHeader, "@@ -1 +1 @@", null, null, 0);

        var line = DiffRowRenderer.Render(row, 24, 3, null, false, GraphGlyphSet.Ascii);

        Assert.Equal("-- @@ -1 +1 @@ ---------", line.Text);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(14)]
    public void GivenAHunkHeaderAndATinyWidth_WhenRendering_ThenTheLineHasExactlyTheWidth(int width)
    {
        var row = new DiffRow(DiffRowKind.HunkHeader, "@@ -1 +1 @@ Foo", null, null, 0);

        var line = DiffRowRenderer.Render(row, width, 3, null, false, Glyphs);

        Assert.Equal(width, TextWidth.Of(line.Text));
    }

    [Fact]
    public void GivenALongHunkHeaderWhenWrapped_WhenRenderingItsSegments_ThenOnlyTheLastOneClosesWithTheRule()
    {
        var row = new DiffRow(DiffRowKind.HunkHeader, "@@ -1 +1 @@ alpha beta gam", null, null, 0);
        var count = DiffRowRenderer.SegmentCount(row, 20, 3);

        var first = DiffRowRenderer.RenderSegment(row, 0, 20, 3, null, false, Glyphs);
        var last = DiffRowRenderer.RenderSegment(row, count - 1, 20, 3, null, false, Glyphs);

        Assert.True(count > 1);
        Assert.StartsWith("── @@", first.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("──────", first.Text, StringComparison.Ordinal);
        Assert.EndsWith("─", last.Text, StringComparison.Ordinal);
        Assert.All(new[] { first, last }, line => Assert.Equal(20, TextWidth.Of(line.Text)));
    }

    [Theory]
    [InlineData(DiffRowKind.FileHeader, SegmentRole.DiffFile)]
    [InlineData(DiffRowKind.HunkHeader, SegmentRole.DiffHunk)]
    [InlineData(DiffRowKind.Warning, SegmentRole.Error)]
    [InlineData(DiffRowKind.Note, SegmentRole.Faint)]
    public void GivenAHeaderRow_WhenRendering_ThenItHasItsRole(DiffRowKind kind, SegmentRole role)
    {
        var line = DiffRowRenderer.Render(new DiffRow(kind, "text", null, null, 0), 30, 3, null, false, Glyphs);

        Assert.Contains(line.Segments, segment => segment.Role == role && segment.Text.Contains("text", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenALargeLineNumber_WhenChoosingTheGutter_ThenItGrows()
    {
        Assert.Equal(3, DiffRowRenderer.GutterDigits(5));
        Assert.Equal(6, DiffRowRenderer.GutterDigits(123456));
    }

    [Fact]
    public void GivenAFileContentRow_WhenRendering_ThenOneNumberColumnAndNoMarkerPrecedeTheText()
    {
        var row = new DiffRow(DiffRowKind.Content, "text", null, 7, 0);

        var line = DiffRowRenderer.Render(row, 20, 3, null, false, Glyphs);

        Assert.StartsWith("  7  text", line.Text);
        Assert.Equal(5, DiffRowRenderer.PrefixWidth(row, 3));
        Assert.Equal(20, TextWidth.Of(line.Text));
    }

    [Fact]
    public void GivenAWrappedFileContentRow_WhenRenderingSegments_ThenOnlyTheFirstShowsTheNumber()
    {
        var row = new DiffRow(DiffRowKind.Content, "abcdefgh", null, 7, 0);

        var first = DiffRowRenderer.RenderSegment(row, 0, 10, 3, null, false, Glyphs);
        var second = DiffRowRenderer.RenderSegment(row, 1, 10, 3, null, false, Glyphs);

        Assert.Equal("  7  abcde", first.Text);
        Assert.Equal("     fgh  ", second.Text);
        Assert.Equal(2, DiffRowRenderer.SegmentCount(row, 10, 3));
    }

    [Fact]
    public void GivenACombinedDiffContextRowWithNoOldNumber_WhenRendering_ThenItKeepsTheFullGutterAndAlignsWithAdded()
    {
        var context = new DiffRow(DiffRowKind.Context, "same", null, 5, 0);
        var added = new DiffRow(DiffRowKind.Added, "same", null, 6, 0);

        var contextLine = DiffRowRenderer.Render(context, 20, 3, null, false, Glyphs);
        var addedLine = DiffRowRenderer.Render(added, 20, 3, null, false, Glyphs);

        Assert.StartsWith("      5  same", contextLine.Text);
        Assert.Equal(DiffRowRenderer.PrefixWidth(added, 3), DiffRowRenderer.PrefixWidth(context, 3));
        Assert.Equal(
            contextLine.Text.IndexOf("same", StringComparison.Ordinal),
            addedLine.Text.IndexOf("same", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenWhitespaceMarksOn_WhenRenderingATabAndTrailingSpaces_ThenMarksReplaceThemInTheSameColumns()
    {
        var row = new DiffRow(DiffRowKind.Added, "a\tb  ", null, 1, 0);

        var plain = DiffRowRenderer.Render(row, 30, 3, null, false, Glyphs);
        var marked = DiffRowRenderer.Render(row, 30, 3, null, false, Glyphs, null, true);

        Assert.Contains("a   b  ", plain.Text, StringComparison.Ordinal);
        Assert.Contains("a→  b••", marked.Text, StringComparison.Ordinal);
        Assert.Equal(TextWidth.Of(plain.Text), TextWidth.Of(marked.Text));
        Assert.Equal(SegmentRole.WhitespaceMark, marked.Segments.First(segment => segment.Text.StartsWith('→')).Role);
        Assert.Equal(SegmentRole.WhitespaceMark, marked.Segments.First(segment => segment.Text == "••").Role);
        Assert.Equal(SegmentRole.DiffAdded, marked.Segments.First(segment => segment.Text.EndsWith('b')).Role);
    }

    [Fact]
    public void GivenWhitespaceMarksOn_WhenALineHasLeadingAndInnerSpaces_ThenEachIsMarkedQuietInTheSameColumns()
    {
        var row = new DiffRow(DiffRowKind.Context, "    one  two", 1, 1, 0);

        var plain = DiffRowRenderer.Render(row, 30, 3, null, false, Glyphs);
        var marked = DiffRowRenderer.Render(row, 30, 3, null, false, Glyphs, null, true);

        Assert.Contains("····one··two", marked.Text, StringComparison.Ordinal);
        Assert.DoesNotContain('•', marked.Text);
        Assert.Equal(TextWidth.Of(plain.Text), TextWidth.Of(marked.Text));
        Assert.Equal(SegmentRole.WhitespaceMark, marked.Segments.First(segment => segment.Text == "····").Role);
        Assert.Equal(SegmentRole.Plain, marked.Segments.First(segment => segment.Text == "one").Role);
    }

    [Fact]
    public void GivenWhitespaceMarksOnInTheAsciiSet_WhenALineHasIndentation_ThenDotsMarkIt()
    {
        var row = new DiffRow(DiffRowKind.Added, "  x", null, 1, 0);

        var marked = DiffRowRenderer.Render(row, 20, 3, null, false, GraphGlyphSet.Ascii, null, true);

        Assert.Contains("..x", marked.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenWhitespaceMarksOn_WhenRenderingTheRowsOfAPane_ThenIndentationOfEveryKindOfRowIsMarked()
    {
        var document = new DiffDocument(
            [
                new DiffRow(DiffRowKind.Context, "    ctx", 1, 1, 0),
                new DiffRow(DiffRowKind.Added, "\tadded  ", null, 2, 0),
                new DiffRow(DiffRowKind.Removed, "  gone", 2, null, 0),
            ],
            [],
            [],
            [["f"]],
            2);
        var pane = new DiffPaneModel(document, 0, 0, false, ShowWhitespace: true);

        var lines = DiffPaneRenderer.RenderRows(pane, 40, 3, Glyphs, 0);

        Assert.Contains("····ctx", lines[0].Text, StringComparison.Ordinal);
        Assert.Contains("→   added••", lines[1].Text, StringComparison.Ordinal);
        Assert.Contains("··gone", lines[2].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenWhitespaceMarksOn_WhenALineEndsInACarriageReturn_ThenItShowsTheMarkInBothGlyphSets()
    {
        var row = new DiffRow(DiffRowKind.Added, "end\r", null, 1, 0);

        var unicode = DiffRowRenderer.Render(row, 20, 3, null, false, GraphGlyphSet.Unicode, null, true);
        var ascii = DiffRowRenderer.Render(row, 20, 3, null, false, GraphGlyphSet.Ascii, null, true);

        Assert.Contains("end¶", unicode.Text, StringComparison.Ordinal);
        Assert.Contains("end<", ascii.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenWhitespaceMarksOnAndAWrappedRow_WhenRenderingEachSegment_ThenMarksSitInTheRightChunk()
    {
        var row = new DiffRow(DiffRowKind.Added, "abcdefgh\tx   ", null, 1, 0);
        var width = DiffRowRenderer.PrefixWidth(row, 3) + 8;

        var texts = Enumerable.Range(0, DiffRowRenderer.SegmentCount(row, width, 3))
            .Select(segment => DiffRowRenderer.RenderSegment(row, segment, width, 3, null, false, Glyphs, null, true))
            .ToList();

        Assert.Contains("abcdefgh", texts[0].Text, StringComparison.Ordinal);
        Assert.Contains("→", texts[1].Text, StringComparison.Ordinal);
        Assert.Equal(SegmentRole.WhitespaceMark, texts[1].Segments.First(segment => segment.Text.StartsWith('→')).Role);
        Assert.Equal(SegmentRole.WhitespaceMark, texts[^1].Segments.First(segment => segment.Text.Contains('•')).Role);
        var plain = Enumerable.Range(0, texts.Count)
            .Select(segment => DiffRowRenderer.RenderSegment(row, segment, width, 3, null, false, Glyphs))
            .ToList();
        Assert.Equal(plain.Select(line => TextWidth.Of(line.Text)), texts.Select(line => TextWidth.Of(line.Text)));
    }

    [Fact]
    public void GivenAVeryLongLineAndMarksOn_WhenCut_ThenNoTrailingMarkAppearsAtTheCut()
    {
        var row = new DiffRow(DiffRowKind.Added, new string(' ', 5000), null, 1, 0);

        var marked = DiffRowRenderer.Render(row, 40, 3, null, false, Glyphs, null, true);

        Assert.Equal(40, TextWidth.Of(marked.Text));
        Assert.DoesNotContain('•', marked.Text);
    }

    [Fact]
    public void GivenWhitespaceMarksOnAndASearch_WhenRendering_ThenTheMatchStillHighlights()
    {
        var row = new DiffRow(DiffRowKind.Added, "find\t me", null, 1, 0);

        var marked = DiffRowRenderer.Render(row, 30, 3, "me", false, Glyphs, null, true);

        Assert.Equal(SegmentRole.SearchMatch, marked.Segments.First(segment => segment.Text == "me").Role);
        Assert.Contains("→", marked.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenWhitespaceMarksOn_WhenALineEndsInSpacesAndACarriageReturn_ThenBothAreMarked()
    {
        var row = new DiffRow(DiffRowKind.Added, "x  \r", null, 1, 0);

        var marked = DiffRowRenderer.Render(row, 20, 3, null, false, Glyphs, null, true);

        Assert.Contains("x••¶", marked.Text, StringComparison.Ordinal);
    }
}
